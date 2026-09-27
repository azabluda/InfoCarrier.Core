// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
///     Records every statement the server's context executes into a <see cref="ServerSqlRecorder" />.
/// </summary>
/// <remarks>
///     <para>
///         <b>An interceptor and not a second <c>LogTo</c>, and that cost a whole comparison run to
///         learn (2026-09-16).</b> <c>DbContextOptionsBuilder.LogTo</c> keeps ONE sink: calling it
///         again REPLACES the first. The recorder was wired with one call and
///         <see cref="ServerSqlLog" /> with another, so switching the log on emptied the recorder,
///         and every test that asserts the server's SQL failed with "the server ran 0 statements"
///         while passing in an ordinary run.
///     </para>
///     <para>
///         <c>AddInterceptors</c> appends, so this and the log coexist. This paragraph went on "and
///         the log keeps the formatted text <c>eng/ef-sql-diff.py</c> reads" until 2026-09-24,
///         because the log stayed on <c>LogTo</c> for that text. It was the same trap the other way
///         round: a fixture's own <c>LogTo</c> displaced the log. The log is an interceptor too
///         now, <see cref="ServerSqlLogInterceptor" />, and writes the same text.
///     </para>
///     <para>
///         <b>It also files each command under <see cref="CurrentTest" /></b>, with its outcome, for
///         the capture of #167: a failed command as well, and a reader's count as EF disposes it.
///         The recorder beside it keeps what it always held, for <c>ServerSqlTest</c>.
///     </para>
/// </remarks>
public sealed class ServerSqlRecordingInterceptor(ServerSqlRecorder recorder) : DbCommandInterceptor
{
    /// <inheritdoc />
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        => Record(command, eventData, count: null, Counted(base.ReaderExecuted(command, eventData, result)));

    /// <inheritdoc />
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
        => Record(command, eventData, count: null, new ValueTask<DbDataReader>(Counted(result)));

    /// <inheritdoc />
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        => Record(command, eventData, result, base.NonQueryExecuted(command, eventData, result));

    /// <inheritdoc />
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
        => Record(command, eventData, result, base.NonQueryExecutedAsync(command, eventData, result, cancellationToken));

    /// <inheritdoc />
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        => Record(command, eventData, count: null, base.ScalarExecuted(command, eventData, result));

    /// <inheritdoc />
    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
        => Record(command, eventData, count: null, base.ScalarExecutedAsync(command, eventData, result, cancellationToken));

    /// <summary>
    ///     Files a command that threw under the current test, marked failed. The recorder never
    ///     held one, and still does not.
    /// </summary>
    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        FileUnderCurrentTest(command, eventData, count: null, failed: true);
        base.CommandFailed(command, eventData);
    }

    /// <inheritdoc cref="CommandFailed" />
    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        FileUnderCurrentTest(command, eventData, count: null, failed: true);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    /// <summary>
    ///     Gives a reader its count as EF disposes it, matched to the command by its correlation ID:
    ///     EF's own <c>ReadCount</c> in a normal run, and <see cref="CountingDataReader.Reads" /> in a
    ///     slow run.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Both count every <c>Read()</c>, the last one that returns false included, which is
    ///         why a capture calls it reads and not rows.
    ///     </para>
    ///     <para>
    ///         <b>EF's count in a normal run</b>, so that no provider meets a reader of a type it did
    ///         not create where nothing compares the count. <b>Ours in a slow run</b>, because EF's
    ///         misses the rows a final <c>GroupBy</c> reads from the raw reader; see
    ///         <see cref="CountingDataReader" />.
    ///     </para>
    /// </remarks>
    public override InterceptionResult DataReaderDisposing(
        DbCommand command,
        DataReaderDisposingEventData eventData,
        InterceptionResult result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        CurrentTest.Value?.SetReadCount(
            eventData.CommandId,
            eventData.DataReader is CountingDataReader counting ? counting.Reads : eventData.ReadCount);
        return base.DataReaderDisposing(command, eventData, result);
    }

    // In a slow run every reader is counted by us, on both halves (#167).
    private static DbDataReader Counted(DbDataReader reader)
        => LiveComparison.IsEnabled ? new CountingDataReader(reader) : reader;

    private T Record<T>(DbCommand command, CommandEventData eventData, int? count, T result)
    {
        ArgumentNullException.ThrowIfNull(command);

        recorder.Add(command.CommandText);
        FileUnderCurrentTest(command, eventData, count, failed: false);
        return result;
    }

    // Filed under the test running in this async flow (#167), and nowhere when none is: a class
    // fixture's seeding belongs to no test.
    private static void FileUnderCurrentTest(DbCommand command, CommandEventData eventData, int? count, bool failed)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(eventData);

        CurrentTest.Value?.Add(new CapturedCommand(
            eventData.CommandId,
            command.CommandText,
            eventData.ExecuteMethod switch
            {
                DbCommandMethod.ExecuteNonQuery => CapturedCommandKind.NonQuery,
                DbCommandMethod.ExecuteScalar => CapturedCommandKind.Scalar,
                _ => CapturedCommandKind.Reader,
            },
            count,
            failed));
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
