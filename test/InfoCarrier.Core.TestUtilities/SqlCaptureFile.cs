// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>One test case of a capture file: its display name without the class, and its ordinal.</summary>
/// <param name="Name">The display name without <c>Ns.Class.</c>: <c>Where_simple(async: True)</c>.</param>
/// <param name="Ordinal">
///     1, unless an earlier case of the class printed the same name: then 2, 3, … in execution order.
/// </param>
public sealed record SqlCaptureCase(string Name, int Ordinal);

/// <summary>One command of an entry: its normalized text, and how it ended.</summary>
/// <param name="Text">The normalized text, which holds no empty line.</param>
/// <param name="Outcome"><c>reads 4</c>, <c>rows 1</c>, <c>scalar</c> or <c>failed</c>.</param>
public sealed record SqlCaptureCommand(string Text, string Outcome);

/// <summary>
///     A capture's <c>.sql</c> file, <c>&lt;Class&gt;.wire.sql</c> or <c>&lt;Class&gt;.direct.sql</c>
///     (#167, <c>docs/sql-capture.md</c> §5). The one reader and the one writer of the format.
/// </summary>
/// <remarks>
///     <para>
///         <b>The format.</b> An entry is its header lines, <c>-- &lt;name&gt;</c> or
///         <c>-- &lt;name&gt; #2</c>, then <c>-- direct run failed</c> if it applies, then for each
///         command <c>-- #n &lt;outcome&gt;</c> and its text. An empty line ends an entry. A header
///         escapes <c>\</c>, CR and LF as <c>\\</c>, <c>\r</c> and <c>\n</c>, so it stays one line.
///     </para>
///     <para>
///         <b>A comment inside a command is never a header</b>, because headers come only before
///         <c>-- #1</c>, and only <c>-- #n+1 </c> ends command n. Text that would read back
///         differently, an empty line or that line, is refused when written.
///     </para>
///     <para>
///         <b>The text is a function of the cases alone</b>: cases whose commands are the same share
///         one entry, headers and entries are sorted ordinally, and nothing else is kept. So a capture
///         that sets some cases leaves every other entry byte for byte, and a diff shows only what
///         changed.
///     </para>
/// </remarks>
public sealed partial class SqlCaptureFile
{
    private const string DirectRunFailedLine = "-- direct run failed";

    private readonly Dictionary<SqlCaptureCase, (IReadOnlyList<SqlCaptureCommand> Commands, bool DirectRunFailed)> _cases = [];

    private SqlCaptureFile()
    {
    }

    /// <summary>Every case the file holds.</summary>
    public IEnumerable<SqlCaptureCase> Cases
        => _cases.Keys;

    /// <summary>Reads the file at <paramref name="path" />. A missing file reads as empty.</summary>
    public static SqlCaptureFile Read(string path)
    {
        var file = new SqlCaptureFile();
        if (!File.Exists(path))
        {
            return file;
        }

        string[] lines = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            if (lines[i].Length == 0)
            {
                i++;
                continue;
            }

            var cases = new List<SqlCaptureCase>();
            bool directRunFailed = false;
            while (i < lines.Length && lines[i].Length > 0 && !StartsCommand(lines[i], 1))
            {
                if (lines[i] == DirectRunFailedLine)
                {
                    directRunFailed = true;
                }
                else if (lines[i].StartsWith("-- ", StringComparison.Ordinal))
                {
                    cases.Add(ParseHeader(lines[i]["-- ".Length..]));
                }
                else
                {
                    throw new FormatException($"{path}, line {i + 1}: a header must start with '-- ', and '{lines[i]}' does not.");
                }

                i++;
            }

            var commands = new List<SqlCaptureCommand>();
            for (int n = 1; i < lines.Length && lines[i].Length > 0; n++)
            {
                if (!StartsCommand(lines[i], n))
                {
                    throw new FormatException($"{path}, line {i + 1}: command #{n} must start with '-- #{n} ', and '{lines[i]}' does not.");
                }

                string outcome = lines[i][$"-- #{n} ".Length..];
                i++;
                var text = new List<string>();
                while (i < lines.Length && lines[i].Length > 0 && !StartsCommand(lines[i], n + 1))
                {
                    text.Add(lines[i]);
                    i++;
                }

                commands.Add(new SqlCaptureCommand(string.Join('\n', text), outcome));
            }

            foreach (SqlCaptureCase testCase in cases)
            {
                file._cases[testCase] = (commands, directRunFailed);
            }
        }

        return file;
    }

    /// <summary>The case's commands, empty for a case that ran none, or null for a case the file lacks.</summary>
    public IReadOnlyList<SqlCaptureCommand>? Find(SqlCaptureCase testCase)
        => _cases.TryGetValue(testCase, out var entry) ? entry.Commands : null;

    /// <summary>Whether the case's test failed in the plain-EF run, which makes its commands a partial reference.</summary>
    public bool DirectRunFailed(SqlCaptureCase testCase)
        => _cases.TryGetValue(testCase, out var entry) && entry.DirectRunFailed;

    /// <summary>Sets the case's entry, leaving every other case as it was.</summary>
    public void Set(SqlCaptureCase testCase, IReadOnlyList<SqlCaptureCommand> commands, bool directRunFailed)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(commands);

        _cases[testCase] = ([.. commands], directRunFailed);
    }

    /// <summary>Writes the file, UTF-8 without a BOM and with LF line ends.</summary>
    /// <exception cref="InvalidOperationException">A command's text would read back differently.</exception>
    public void Write(string path)
    {
        var text = new StringBuilder();
        foreach (var (headers, body) in _cases
            .GroupBy(pair => Body(pair.Key, pair.Value.Commands, pair.Value.DirectRunFailed), StringComparer.Ordinal)
            .Select(group => (Headers: group.Select(pair => Header(pair.Key)).Order(StringComparer.Ordinal).ToList(), Body: group.Key))
            .OrderBy(entry => entry.Headers[0], StringComparer.Ordinal))
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }

            foreach (string header in headers)
            {
                text.Append(header).Append('\n');
            }

            text.Append(body);
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Checks that the case's commands can be written so that they read back the same.</summary>
    /// <exception cref="InvalidOperationException">A command's text holds an empty line, a CR, or a line that starts the next command.</exception>
    public static void EnsureWritable(SqlCaptureCase testCase, IReadOnlyList<SqlCaptureCommand> commands)
        => _ = Body(testCase, commands, directRunFailed: false);

    private static string Body(SqlCaptureCase testCase, IReadOnlyList<SqlCaptureCommand> commands, bool directRunFailed)
    {
        var body = new StringBuilder();
        if (directRunFailed)
        {
            body.Append(DirectRunFailedLine).Append('\n');
        }

        for (int n = 1; n <= commands.Count; n++)
        {
            (string text, string outcome) = commands[n - 1];
            if (outcome.Contains('\n', StringComparison.Ordinal) || outcome.Contains('\r', StringComparison.Ordinal))
            {
                throw Unreadable(testCase, n, "its outcome holds a line break");
            }

            body.Append($"-- #{n} {outcome}\n");
            if (text.Length == 0)
            {
                continue;
            }

            foreach (string line in text.Split('\n'))
            {
                if (line.Length == 0 || line.Contains('\r', StringComparison.Ordinal))
                {
                    throw Unreadable(testCase, n, "its text holds an empty line or a CR, and an empty line ends an entry");
                }

                if (StartsCommand(line, n + 1))
                {
                    throw Unreadable(testCase, n, $"its text holds a line that starts '-- #{n + 1} ', which starts the next command");
                }

                body.Append(line).Append('\n');
            }
        }

        return body.ToString();
    }

    private static InvalidOperationException Unreadable(SqlCaptureCase testCase, int n, string why)
        => new($"Command #{n} of '{testCase.Name}' cannot be written so that it reads back the same: {why}.");

    private static bool StartsCommand(string line, int n)
        => line.StartsWith($"-- #{n} ", StringComparison.Ordinal);

    private static string Header(SqlCaptureCase testCase)
    {
        string name = testCase.Name
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return testCase.Ordinal == 1 ? $"-- {name}" : $"-- {name} #{testCase.Ordinal}";
    }

    private static SqlCaptureCase ParseHeader(string header)
    {
        int ordinal = 1;
        Match suffix = OrdinalSuffix().Match(header);
        if (suffix.Success)
        {
            ordinal = int.Parse(suffix.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            header = header[..suffix.Index];
        }

        var name = new StringBuilder(header.Length);
        for (int i = 0; i < header.Length; i++)
        {
            if (header[i] == '\\' && i + 1 < header.Length && header[i + 1] is '\\' or 'r' or 'n')
            {
                name.Append(header[++i] switch { 'r' => '\r', 'n' => '\n', _ => '\\' });
            }
            else
            {
                name.Append(header[i]);
            }
        }

        return new SqlCaptureCase(name.ToString(), ordinal);
    }

    [GeneratedRegex(@" #([0-9]+)$")]
    private static partial Regex OrdinalSuffix();
}
