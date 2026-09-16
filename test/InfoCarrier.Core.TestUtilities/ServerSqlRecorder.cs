// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The statements the <em>server</em> ran, per store, so a test can assert them as EF's own
///     provider tests do (#111).
/// </summary>
/// <remarks>
///     <para>
///         <b>One recorder per store, and a store belongs to one fixture</b>, which is how EF keeps
///         its own <c>TestSqlLoggerFactory</c> honest while test classes run in parallel: a class has
///         its own fixture, and xUnit runs the tests of one class one after another. Each test class
///         that asserts clears the recorder in its constructor, exactly as EF's SQLite classes call
///         <c>Fixture.TestSqlLoggerFactory.Clear()</c>.
///     </para>
///     <para>
///         <b>Not the client's <c>TestSqlLoggerFactory</c>, on purpose.</b> That one belongs to the
///         client, which emits no SQL, and several specification bases assert on its contents with
///         <c>Assert.Single</c>; putting the server's statements there would change what those tests
///         see. This is a second, separate sink.
///     </para>
/// </remarks>
public sealed class ServerSqlRecorder
{
    private readonly object _gate = new();
    private readonly List<string> _statements = [];

    /// <summary>The statements since the last <see cref="Clear" />, in the order the store ran them.</summary>
    public IReadOnlyList<string> Statements
    {
        get
        {
            lock (_gate)
            {
                return [.. _statements];
            }
        }
    }

    /// <summary>Records one statement.</summary>
    public void Add(string commandText)
    {
        lock (_gate)
        {
            _statements.Add(commandText);
        }
    }

    /// <summary>Forgets what has been recorded. A test class calls this in its constructor.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _statements.Clear();
        }
    }
}

/// <summary>
///     Asserts the statements the server ran against the text EF's own provider test expects.
/// </summary>
/// <remarks>
///     <para>
///         <b>What is ignored is names, and nothing else</b>: whitespace, parameter names and column
///         aliases. A parameter crosses inside <c>ParameterBox&lt;T&gt;</c>, so EF names it after the
///         box's property, and the projection split names a column <c>Item1</c> where the caller's
///         projection called it <c>Id</c>. Literals, structure and the order of the statements are
///         compared. <c>eng/ef-sql-diff.py</c> reads EF's text the same way.
///     </para>
///     <para>
///         <b>EF's parameter preamble is dropped from the expected text.</b> EF prints the parameter
///         values above each statement; the values are what the test's own assertions are about, and
///         the names are not comparable here.
///     </para>
/// </remarks>
public static partial class ServerSqlAssertions
{
    /// <summary>
    ///     The recorder of the server behind <paramref name="testStore" />, whether that is the
    ///     backend store itself or the client shell a fixture puts in front of it.
    /// </summary>
    public static ServerSqlRecorder ServerSqlRecorderOf(this TestStore testStore)
        => testStore switch
        {
            InfoCarrierBackendTestStore backend => backend.ServerSql,
            IInfoCarrierClientTestStore client => client.Backend.ServerSql,
            _ => throw new InvalidOperationException(
                $"'{testStore?.GetType().Name}' remotes to no InfoCarrier server, so it records no server SQL."),
        };

    /// <summary>
    ///     Asserts that the server ran exactly <paramref name="expected" />, EF's own text for the
    ///     same test.
    /// </summary>
    public static void AssertServerSql(this TestStore testStore, params string[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        IReadOnlyList<string> actual = testStore.ServerSqlRecorderOf().Statements;
        string[] wanted = [.. expected.Select(WithoutParameterPreamble).Select(Canonical)];
        string[] ran = [.. actual.Select(Canonical)];

        if (!wanted.SequenceEqual(ran, StringComparer.Ordinal))
        {
            Assert.Fail(
                $"The server ran {ran.Length} statement(s) where EF's own provider runs {wanted.Length}, or they differ."
                + Environment.NewLine
                + Environment.NewLine + "Expected (EF's, names ignored):" + Environment.NewLine
                + string.Join(Environment.NewLine + "--" + Environment.NewLine, wanted) + Environment.NewLine
                + Environment.NewLine + "Actual (the server's, names ignored):" + Environment.NewLine
                + string.Join(Environment.NewLine + "--" + Environment.NewLine, ran) + Environment.NewLine
                + Environment.NewLine + "Actual (as the server ran it):" + Environment.NewLine
                + string.Join(Environment.NewLine + "--" + Environment.NewLine, actual));
        }
    }

    /// <summary>EF's expected text without the parameter values it prints above the statement.</summary>
    private static string WithoutParameterPreamble(string expected)
    {
        string[] lines = expected.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int blank = Array.IndexOf(lines, string.Empty);
        return lines.Length > 0 && lines[0].StartsWith('@') && blank > 0
            ? string.Join('\n', lines[(blank + 1)..])
            : expected;
    }

    private static string Canonical(string sql)
        => ColumnAlias().Replace(
            ParameterName().Replace(Whitespace().Replace(sql, " ").Trim(), "@p"),
            string.Empty);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"@[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex ParameterName();

    [GeneratedRegex(@"\s+AS ""[^""]*""")]
    private static partial Regex ColumnAlias();
}
