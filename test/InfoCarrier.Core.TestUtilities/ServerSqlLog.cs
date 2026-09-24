// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     An opt-in record of the SQL the <em>server</em> ran, for diagnosing a failing Tier B test.
/// </summary>
/// <remarks>
///     <para>
///         <b>Off unless <c>INFOCARRIER_SERVER_SQL</c> is set, and it asserts nothing.</b> Its
///         whole job is that a failing test can be re-run with the switch on and the statements
///         read back. Until #59 there was no way to see them at all:
///         <see cref="InfoCarrierTestStoreFactory" /> builds a
///         <c>TestSqlLoggerFactory</c>, but it belongs to the <em>client</em>, which has no
///         database and emits none, and <see cref="InfoCarrierBackendTestStore" /> wired no logger.
///     </para>
///     <para>
///         <b>Why a file and a switch rather than test output on failure.</b> EF attaches its own
///         SQL to a failing test through <c>TestSqlLoggerFactory.SetTestOutputHelper</c>, which
///         every relational base takes an <c>ITestOutputHelper</c> to supply. Reaching that here
///         would mean threading one through every Tier B test class, and xUnit gives a fixture no
///         way to know a test failed. A switch costs nothing when off, and the diagnostic loop —
///         re-run the one failing test with a filter, read the file — is the same loop anyone is
///         already in.
///     </para>
///     <para>
///         This is diagnosis, not coverage, and it is what <c>eng/ef-sql-compare.sh</c> reads. The
///         tests that ASSERT the server's SQL are <c>ServerSqlTest</c>, which states this provider's
///         promises with its own model and its own expected text, and
///         <c>ServerParameterizationTest</c>, which compares a statement against the same query run
///         directly and needs no text at all.
///     </para>
/// </remarks>
public static class ServerSqlLog
{
    private static readonly object Gate = new();

    /// <summary>
    ///     Whether server SQL is being recorded.
    /// </summary>
    public static bool IsEnabled { get; }
        = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INFOCARRIER_SERVER_SQL"));

    /// <summary>
    ///     Where the statements are written: the value of <c>INFOCARRIER_SERVER_SQL</c> when it names
    ///     a file, and <c>server-sql.log</c> in the test output directory otherwise.
    /// </summary>
    /// <remarks>
    ///     <b>A path, so that a run can write somewhere new.</b> The file is appended to and
    ///     truncated once per process, so a second run in the same output directory reuses the same
    ///     file, and a stale one from an earlier run is easy to read as this run's.
    ///     <c>eng/ef-sql-compare.sh</c> passes a fresh temporary path for exactly that reason. A
    ///     value that is not a path, <c>1</c> for instance, only turns the log on.
    /// </remarks>
    public static string Path { get; }
        = Environment.GetEnvironmentVariable("INFOCARRIER_SERVER_SQL") is { } value
            && value.IndexOfAny([System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar]) >= 0
                ? value
                : System.IO.Path.Combine(AppContext.BaseDirectory, "server-sql.log");

    private static bool _started;

    /// <summary>
    ///     Records one statement.
    /// </summary>
    /// <param name="line">
    ///     The formatted log line, in the text EF's own <c>LogTo</c> writes. It came from
    ///     <c>LogTo</c> itself until 2026-09-24 and comes from <see cref="ServerSqlLogInterceptor" />
    ///     now; see that class for why.
    /// </param>
    public static void Write(string line)
    {
        lock (Gate)
        {
            if (!_started)
            {
                System.IO.File.WriteAllText(Path, string.Empty);
                _started = true;
            }

            System.IO.File.AppendAllText(Path, line + Environment.NewLine + Environment.NewLine);
        }
    }

    /// <summary>
    ///     Writes the line that says which test the statements after it belong to.
    /// </summary>
    /// <param name="test">The test's class and method.</param>
    public static void WriteTestMarker(string test)
        => Write(TestMarker + test);

    /// <summary>The prefix of a test marker line, which <c>eng/ef-sql-diff.py</c> reads.</summary>
    public const string TestMarker = "=== TEST ";
}

/// <summary>
///     Marks each test's start in <see cref="ServerSqlLog" />, so the statements after it are known
///     to be that test's.
/// </summary>
/// <remarks>
///     <para>
///         <b>Applied to the assembly</b>, in <c>ServerSqlLogAssemblyInfo.cs</c>, and inert unless
///         <c>INFOCARRIER_SERVER_SQL</c> is set. It is what makes <c>eng/ef-sql-diff.py</c> possible:
///         the log is otherwise one stream with nothing saying where a test begins.
///     </para>
///     <para>
///         <b>A comparison run has to be serial</b> —
///         <c>dotnet test … -- xUnit.ParallelizeTestCollections=false</c> — or the statements of two
///         tests interleave between two markers. That is the caller's business, not this attribute's:
///         a parallel run with the switch on is still a usable diagnostic, as it was before markers
///         existed.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ServerSqlTestMarkerAttribute : Xunit.Sdk.BeforeAfterTestAttribute
{
    /// <inheritdoc />
    public override void Before(System.Reflection.MethodInfo methodUnderTest)
    {
        ArgumentNullException.ThrowIfNull(methodUnderTest);

        if (ServerSqlLog.IsEnabled)
        {
            ServerSqlLog.WriteTestMarker($"{methodUnderTest.ReflectedType?.FullName}.{methodUnderTest.Name}");
        }
    }
}

/// <summary>
///     Writes every statement the server's context executes, and every one that fails, to
///     <see cref="ServerSqlLog" /> in the text EF's own <c>LogTo</c> writes.
/// </summary>
/// <remarks>
///     <para>
///         <b>An interceptor and not <c>LogTo</c>, since 2026-09-24, for the reason
///         <see cref="ServerSqlRecordingInterceptor" /> is one.</b> <c>LogTo</c> keeps ONE sink,
///         and a later call replaces it. The store wired this log with <c>LogTo</c>, and a fixture
///         whose <see cref="SharedTestStoreProperties.OnAddOptions" /> called <c>LogTo</c> as well
///         replaced it without a word. Measured 2026-09-24: a run of the whole of
///         <c>ServerParameterizationTest</c>, which does exactly that, wrote 58 test markers and 0
///         statements. <c>AddInterceptors</c> appends, so nothing a fixture adds can displace this
///         one, and a fixture can still use <c>LogTo</c> for itself.
///     </para>
///     <para>
///         <b>The text is <c>LogTo</c>'s, rebuilt from public members</b>, because
///         <c>eng/ef-sql-diff.py</c> reads it: a header of level, local time, event id and category,
///         then the message, which is <see cref="EventData.ToString" />, indented by six spaces. EF's
///         own formatter is <c>FormattingDbContextLogger</c>, in an <c>Internal</c> namespace, and
///         the format is short enough to state here. <c>ServerSqlLogTest</c> pins it against the
///         script's own reading.
///     </para>
///     <para>
///         <b>A failed statement is written too</b>, as <c>LogTo</c> wrote <c>CommandError</c>: it is
///         the one a diagnosis needs most, and <c>CommandExecuted</c> never carries it.
///     </para>
/// </remarks>
/// <param name="sink">Where each formatted entry goes.</param>
public sealed class ServerSqlLogInterceptor(Action<string> sink) : DbCommandInterceptor
{
    private const string Padding = "      ";

    /// <summary>
    ///     Writes to <see cref="ServerSqlLog" />.
    /// </summary>
    public ServerSqlLogInterceptor()
        : this(ServerSqlLog.Write)
    {
    }

    /// <inheritdoc />
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        => Log(eventData, base.ReaderExecuted(command, eventData, result));

    /// <inheritdoc />
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
        => Log(eventData, base.ReaderExecutedAsync(command, eventData, result, cancellationToken));

    /// <inheritdoc />
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        => Log(eventData, base.NonQueryExecuted(command, eventData, result));

    /// <inheritdoc />
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
        => Log(eventData, base.NonQueryExecutedAsync(command, eventData, result, cancellationToken));

    /// <inheritdoc />
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        => Log(eventData, base.ScalarExecuted(command, eventData, result));

    /// <inheritdoc />
    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
        => Log(eventData, base.ScalarExecutedAsync(command, eventData, result, cancellationToken));

    /// <inheritdoc />
    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        Log(eventData);
        base.CommandFailed(command, eventData);
    }

    /// <inheritdoc />
    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Log(eventData);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private T Log<T>(EventData eventData, T result)
    {
        Log(eventData);
        return result;
    }

    private void Log(EventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        string name = eventData.EventId.Name ?? string.Empty;
        int lastDot = name.LastIndexOf('.');
        string category = lastDot > 0 ? $"({name[..lastDot]}) " : string.Empty;
        string time = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture);

        sink(
            $"{Level(eventData.LogLevel)}: {time} {eventData.EventIdCode}[{eventData.EventId.Id}] {category}"
            + Environment.NewLine
            + Padding
            + eventData.ToString().Replace(Environment.NewLine, Environment.NewLine + Padding, StringComparison.Ordinal));
    }

    private static string Level(LogLevel level)
        => level switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "none",
        };
}
