// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     The server SQL log survives a fixture's own <c>LogTo</c>, and writes the text
///     <c>eng/ef-sql-diff.py</c> reads.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a test and not only a comment.</b> The log is switched on by an environment
///         variable that no ordinary run sets, so nothing else in the suite ever runs
///         <see cref="ServerSqlLogInterceptor" />. Its text is also a contract with a Python script
///         that has no test of its own. Both halves broke silently before: a fixture's
///         <c>LogTo</c> emptied the log of a whole class (2026-09-23), and the recorder the same
///         way once before (2026-09-16).
///     </para>
///     <para>
///         <b>The first test is a control, and it asserts EF's behaviour, not ours.</b> It shows the
///         trap is real in the EF Core this suite runs. If EF ever keeps both sinks, it fails, and
///         the interceptor is then a choice rather than a necessity.
///     </para>
/// </remarks>
public class ServerSqlLogTest
{
    private const string Padding = "      ";

    [Fact]
    public async Task A_second_LogTo_replaces_the_first()
    {
        List<string> first = [];
        List<string> second = [];

        await RunAsync(
            b => b
                .LogTo(first.Add, [RelationalEventId.CommandExecuted])
                .LogTo(second.Add, [RelationalEventId.CommandExecuted]),
            "SELECT 1");

        Assert.Empty(first);
        Assert.Single(second);
    }

    [Fact]
    public async Task A_fixtures_own_LogTo_does_not_displace_the_log()
    {
        List<string> log = [];
        List<string> fixture = [];

        await RunAsync(
            b => b
                .AddInterceptors(new ServerSqlLogInterceptor(log.Add))
                .LogTo(fixture.Add, [RelationalEventId.CommandExecuted]),
            "SELECT 1");

        Assert.Single(log);
        Assert.Single(fixture);
    }

    /// <summary>
    ///     An executed statement is written as <c>LogTo</c> writes it, which is what
    ///     <c>server_sections</c> in <c>eng/ef-sql-diff.py</c> reads.
    /// </summary>
    /// <remarks>
    ///     The script starts an entry at a line matching its <c>LOG_HEADER</c>, finds the line
    ///     that says <c>Executed DbCommand</c>, and takes every line after it, less six spaces, as
    ///     the statement. The pattern below is the script's own, copied.
    /// </remarks>
    [Fact]
    public async Task An_executed_statement_is_written_in_the_text_the_script_reads()
    {
        List<string> log = [];

        await RunAsync(b => b.AddInterceptors(new ServerSqlLogInterceptor(log.Add)), "SELECT 1");

        string[] lines = Assert.Single(log).Split(Environment.NewLine);
        Assert.Matches(@"^\w+: \d\d/\d\d/\d{4} ", lines[0]);
        Assert.Contains("RelationalEventId.CommandExecuted", lines[0], StringComparison.Ordinal);
        Assert.StartsWith(Padding + "Executed DbCommand", lines[1], StringComparison.Ordinal);
        Assert.Equal(Padding + "SELECT 1", lines[2]);
    }

    /// <summary>
    ///     A statement that fails is written too, as <c>LogTo</c> wrote <c>CommandError</c>.
    /// </summary>
    [Fact]
    public async Task A_failed_statement_is_written_too()
    {
        List<string> log = [];

        await Assert.ThrowsAnyAsync<SqliteException>(
            () => RunAsync(
                b => b.AddInterceptors(new ServerSqlLogInterceptor(log.Add)),
                "SELECT * FROM \"NoSuchTable\""));

        string[] lines = Assert.Single(log).Split(Environment.NewLine);
        Assert.StartsWith("fail: ", lines[0], StringComparison.Ordinal);
        Assert.Contains("RelationalEventId.CommandError", lines[0], StringComparison.Ordinal);
        Assert.StartsWith(Padding + "Failed executing DbCommand", lines[1], StringComparison.Ordinal);
        Assert.Equal(Padding + "SELECT * FROM \"NoSuchTable\"", lines[2]);
    }

    private static async Task RunAsync(Func<DbContextOptionsBuilder, DbContextOptionsBuilder> configure, string sql)
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        await using DbContext context = new(configure(new DbContextOptionsBuilder().UseSqlite(connection)).Options);
        _ = await context.Database.ExecuteSqlRawAsync(sql);
    }
}
