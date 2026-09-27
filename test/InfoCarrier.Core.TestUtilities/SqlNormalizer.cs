// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     Makes the names in a command text positional, so a comparison sees the statement's shape and
///     not the names a client happened to choose (#167, ADR-014).
/// </summary>
/// <remarks>
///     <para>
///         <b>What is a name here, and why.</b> A parameter reaches the server inside a
///         <c>ParameterBox&lt;T&gt;</c>, so EF names it <c>@Value</c> where the caller wrote
///         <c>city</c>. The projection split names a column <c>Item1</c> where the caller's
///         projection called it <c>Title</c>, and <b>EF writes no <c>AS</c> when an alias equals the
///         column's own name</b>, so InfoCarrier's <c>"c"."Id" AS "Item1"</c> is plain EF's
///         <c>"c"."Id"</c>: a column alias cannot be numbered, because one side has none. It goes,
///         and a column read through a derived table is numbered within its table instead.
///     </para>
///     <para>
///         <b>What stays</b>: literals, comments, keywords, a base table's columns, and the
///         structure. A literal where EF has a parameter, or the reverse, is a defect, and a
///         structural change is what the comparison is for.
///     </para>
///     <para>
///         <b>A lexer and not a regular expression</b>, because <c>AS "x"</c>, <c>@name</c> and a
///         quote can all sit inside a string literal or a <c>TagWith</c> comment, and those pass
///         through unchanged.
///     </para>
///     <para>
///         <b>A known limit, pinned in <c>SqlNormalizerTest</c></b>: a derived table's columns are
///         numbered in the order they are first read. Two statements that read the same two columns
///         in the opposite order, and filter on the first one read, therefore normalize equal though
///         they filter on different columns. Numbering by the derived table's own projection would
///         close it, and was left for when a real pair of statements needs it (review, 2026-09-26).
///     </para>
/// </remarks>
public static class SqlNormalizer
{
    /// <summary>
    ///     A table alias is an <c>AS "x"</c> in these clauses; its columns are qualified with it.
    /// </summary>
    private static readonly HashSet<string> TableClauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "FROM", "JOIN", "APPLY", "UPDATE",
    };

    /// <summary>A column alias is an <c>AS "x"</c> in these clauses, and it goes.</summary>
    private static readonly HashSet<string> ColumnClauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "RETURNING",
    };

    /// <summary>
    ///     The keywords that start a clause, so an <c>AS</c> can tell which clause it is in at its
    ///     own parenthesis depth.
    /// </summary>
    private static readonly HashSet<string> ClauseKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "JOIN", "APPLY", "UPDATE", "RETURNING", "WHERE", "ON", "SET", "GROUP", "ORDER",
        "HAVING", "LIMIT", "OFFSET", "VALUES", "INSERT", "DELETE", "UNION", "EXCEPT", "INTERSECT",
    };

    private enum Kind
    {
        QuotedIdentifier,
        Literal,
        Comment,
        Parameter,
        Word,
        Punctuation,
        Whitespace,
    }

    /// <summary>
    ///     The command text with its parameters, table aliases and derived-table columns numbered by
    ///     first appearance, its column aliases removed, and its lines trimmed, LF-ended and never
    ///     empty.
    /// </summary>
    public static string Normalize(string commandText)
    {
        ArgumentNullException.ThrowIfNull(commandText);

        List<(Kind Kind, string Text)> tokens = Tokenize(commandText);
        (Dictionary<string, bool> derivedByAlias, HashSet<int> aliasDefinitions, HashSet<int> removed) = Classify(tokens);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var columns = new Dictionary<(string Alias, string Column), string>();
        var columnCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var output = new StringBuilder(commandText.Length);

        for (int i = 0; i < tokens.Count; i++)
        {
            if (removed.Contains(i))
            {
                continue;
            }

            (Kind kind, string text) = tokens[i];
            switch (kind)
            {
                case Kind.Parameter:
                    output.Append(Number(parameters, text, "@p"));
                    break;

                case Kind.QuotedIdentifier when aliasDefinitions.Contains(i):
                    output.Append(Quote(Number(aliases, Unquote(text), "t")));
                    break;

                case Kind.QuotedIdentifier when IsQualifier(tokens, i) && derivedByAlias.TryGetValue(Unquote(text), out bool derived):
                    string alias = Unquote(text);
                    output.Append(Quote(Number(aliases, alias, "t"))).Append('.');
                    i++;
                    if (derived && i + 1 < tokens.Count && tokens[i + 1].Kind == Kind.QuotedIdentifier)
                    {
                        i++;
                        (string, string) key = (alias, Unquote(tokens[i].Text));
                        if (!columns.TryGetValue(key, out string? column))
                        {
                            int next = columnCounts.GetValueOrDefault(alias);
                            columnCounts[alias] = next + 1;
                            column = $"c{next}";
                            columns[key] = column;
                        }

                        output.Append(Quote(column));
                    }

                    break;

                default:
                    output.Append(text);
                    break;
            }
        }

        return string.Join(
            '\n',
            output.ToString()
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.TrimEnd(' ', '\t'))
                .Where(line => line.Length > 0));
    }

    /// <summary>
    ///     Finds each <c>AS "x"</c> and what it names: a table alias, derived when a parenthesis
    ///     closes before it, or a column alias, whose tokens are marked for removal.
    /// </summary>
    private static (Dictionary<string, bool> DerivedByAlias, HashSet<int> AliasDefinitions, HashSet<int> Removed) Classify(
        List<(Kind Kind, string Text)> tokens)
    {
        var derivedByAlias = new Dictionary<string, bool>(StringComparer.Ordinal);
        var aliasDefinitions = new HashSet<int>();
        var removed = new HashSet<int>();
        var clauses = new Stack<string?>();
        clauses.Push(null);

        for (int i = 0; i < tokens.Count; i++)
        {
            (Kind kind, string text) = tokens[i];
            if (kind == Kind.Punctuation && text == "(")
            {
                clauses.Push(null);
            }
            else if (kind == Kind.Punctuation && text == ")")
            {
                if (clauses.Count > 1)
                {
                    clauses.Pop();
                }
            }
            else if (kind == Kind.Word && ClauseKeywords.Contains(text))
            {
                clauses.Pop();
                clauses.Push(text);
            }
            else if (kind == Kind.Word
                && text.Equals("AS", StringComparison.OrdinalIgnoreCase)
                && clauses.Peek() is { } clause
                && NextSignificant(tokens, i) is var name
                && name >= 0
                && tokens[name].Kind == Kind.QuotedIdentifier)
            {
                if (TableClauses.Contains(clause))
                {
                    int before = PreviousSignificant(tokens, i);
                    derivedByAlias[Unquote(tokens[name].Text)] =
                        before >= 0 && tokens[before] is (Kind.Punctuation, ")");
                    aliasDefinitions.Add(name);
                }
                else if (ColumnClauses.Contains(clause))
                {
                    int from = i;
                    if (i > 0 && tokens[i - 1] is (Kind.Whitespace, var space) && !space.Contains('\n', StringComparison.Ordinal))
                    {
                        from = i - 1;
                    }

                    for (int j = from; j <= name; j++)
                    {
                        removed.Add(j);
                    }

                    i = name;
                }
            }
        }

        return (derivedByAlias, aliasDefinitions, removed);
    }

    private static List<(Kind Kind, string Text)> Tokenize(string text)
    {
        var tokens = new List<(Kind, string)>();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            int start = i;
            Kind kind;
            if (c is '"' or '\'')
            {
                // A doubled quote inside is the quote itself, so only a lone one ends the token.
                i++;
                while (i < text.Length && !(text[i] == c && (i + 1 >= text.Length || text[i + 1] != c)))
                {
                    i += text[i] == c ? 2 : 1;
                }

                i = Math.Min(i + 1, text.Length);
                kind = c == '"' ? Kind.QuotedIdentifier : Kind.Literal;
            }
            else if (c == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                while (i < text.Length && text[i] is not ('\n' or '\r'))
                {
                    i++;
                }

                kind = Kind.Comment;
            }
            else if (c == '@' && i + 1 < text.Length && IsWordChar(text[i + 1]))
            {
                i++;
                while (i < text.Length && IsWordChar(text[i]))
                {
                    i++;
                }

                kind = Kind.Parameter;
            }
            else if (IsWordChar(c))
            {
                while (i < text.Length && IsWordChar(text[i]))
                {
                    i++;
                }

                kind = Kind.Word;
            }
            else if (char.IsWhiteSpace(c))
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                kind = Kind.Whitespace;
            }
            else
            {
                i++;
                kind = Kind.Punctuation;
            }

            tokens.Add((kind, text[start..i]));
        }

        return tokens;
    }

    private static bool IsWordChar(char c)
        => char.IsLetterOrDigit(c) || c is '_' or '$';

    private static bool IsQualifier(List<(Kind Kind, string Text)> tokens, int i)
        => i + 1 < tokens.Count && tokens[i + 1] is (Kind.Punctuation, ".");

    private static int NextSignificant(List<(Kind Kind, string Text)> tokens, int i)
    {
        for (int j = i + 1; j < tokens.Count; j++)
        {
            if (tokens[j].Kind != Kind.Whitespace)
            {
                return j;
            }
        }

        return -1;
    }

    private static int PreviousSignificant(List<(Kind Kind, string Text)> tokens, int i)
    {
        for (int j = i - 1; j >= 0; j--)
        {
            if (tokens[j].Kind != Kind.Whitespace)
            {
                return j;
            }
        }

        return -1;
    }

    private static string Number(Dictionary<string, string> names, string name, string prefix)
    {
        if (!names.TryGetValue(name, out string? number))
        {
            number = $"{prefix}{names.Count}";
            names[name] = number;
        }

        return number;
    }

    // An unterminated quote runs to the end of the text, and is left as it is.
    private static string Unquote(string quoted)
        => quoted.Length >= 2 && quoted[^1] == '"'
            ? quoted[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal)
            : quoted;

    private static string Quote(string name)
        => $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
