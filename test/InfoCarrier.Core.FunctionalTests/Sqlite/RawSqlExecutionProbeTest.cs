// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     What a raw SQL string actually gets to do once it reaches the server (#60, R94).
/// </summary>
/// <remarks>
///     <para>
///         <b>This class asserts facts about <c>Microsoft.Data.Sqlite</c> and about EF Core, not
///         about this provider.</b> Nothing here crosses the wire. It exists because the design of
///         a <c>FromSql</c> opt-in gate rests on two questions whose answers were assumed and had
///         never been measured, and a fact stated in prose goes stale where a test does not.
///     </para>
///     <para>
///         <b>Question 1 - does one <c>CommandText</c> execute more than one statement?</b>
///         Answered by <see cref="A_single_CommandText_executes_every_statement_it_contains" />:
///         <b>yes</b>. <c>SELECT 1; DROP TABLE Probe;</c> drops the table, on
///         <c>ExecuteNonQuery</c> and on the <c>ExecuteReader</c> path EF itself uses. For the
///         reader the trailing statements run as the reader is advanced past the first result set,
///         which disposal does - so a caller who reads one row and stops has still run the
///         <c>DROP</c>. <c>Microsoft.Data.Sqlite</c> prepares and runs the statements in sequence
///         by design; there is no single-statement mode to switch on.
///     </para>
///     <para>
///         <b>Question 2 - does EF hand the caller's string to the store unwrapped?</b> Answered by
///         <see cref="An_uncomposed_FromSqlRaw_reaches_the_store_unwrapped" /> and
///         <see cref="A_composed_FromSqlRaw_is_wrapped_in_a_subquery" />: <b>an uncomposed
///         <c>FromSqlRaw</c> is the whole command, verbatim</b>. Compose anything over it - a
///         <c>Where</c>, an <c>OrderBy</c> - and EF wraps it as <c>FROM (&lt;sql&gt;) AS x</c>
///         instead. The wrap is therefore not a safety property: it is an artefact of composition,
///         and the caller is the one who decides whether to compose.
///     </para>
///     <para>
///         <b>What the two answers mean together, and it is the reason this file exists.</b>
///         Enabling <c>FromSql</c> on a server enables <em>arbitrary SQL execution</em> on that
///         server's connection, under whatever rights it holds. There is no read-only subset of the
///         feature to grant, because the wrap that would have produced one is optional from the
///         caller's side and the store executes every statement regardless. Any opt-in registration
///         must therefore be named and documented for what it grants - see
///         <c>docs/security-review.md</c> section 5.
///     </para>
/// </remarks>
public class RawSqlExecutionProbeTest
{
    // QUESTION 1, on the driver alone: no EF, no wire. ANSWER: yes, every statement runs.
    //
    // Both execution paths are measured, because they are not the same code in the driver and only
    // the second is the one EF takes for a query.
    [ConditionalFact]
    public void A_single_CommandText_executes_every_statement_it_contains()
    {
        using SqliteConnection connection = OpenProbeDatabase();

        Execute(connection, "CREATE TABLE Probe (Id integer primary key)");
        Assert.True(TableExists(connection, "Probe"));

        using (SqliteCommand nonQuery = connection.CreateCommand())
        {
            nonQuery.CommandText = "SELECT 1; DROP TABLE Probe;";
            nonQuery.ExecuteNonQuery();
        }

        Assert.False(TableExists(connection, "Probe"));

        // And again on the reader path, where the caller never asks for the second result set.
        Execute(connection, "CREATE TABLE Probe (Id integer primary key)");
        Assert.True(TableExists(connection, "Probe"));

        using (SqliteCommand reader = connection.CreateCommand())
        {
            reader.CommandText = "SELECT 1; DROP TABLE Probe;";
            using SqliteDataReader rows = reader.ExecuteReader();
            Assert.True(rows.Read());
            Assert.Equal(1L, rows.GetInt64(0));
        }

        Assert.False(TableExists(connection, "Probe"));
    }

    // QUESTION 2a. ANSWER: unwrapped. The caller's string IS the command, character for character.
    [ConditionalFact]
    public async Task An_uncomposed_FromSqlRaw_reaches_the_store_unwrapped()
    {
        using SqliteConnection connection = OpenProbeDatabase();
        await using ProbeContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        const string Sql = @"SELECT ""Id"", ""Title"" FROM ""Rows"" WHERE ""Id"" = 1";

        // Trimmed, and that is the only difference: `ToQueryString` ends every statement it
        // renders with a newline. What sits before it is the caller's string character for
        // character - no SELECT around it, no alias, no added predicate.
        Assert.Equal(Sql, context.Rows.FromSqlRaw(Sql).ToQueryString().Trim());

        // ToQueryString reports what EF *would* send, so the query is run as well and the
        // assertion does not rest on a code path the execution does not take.
        Assert.Empty(await context.Rows.FromSqlRaw(Sql).ToListAsync());
    }

    // QUESTION 2b, the contrast that makes 2a mean something: the wrapping does exist, it is just
    // not a property of `FromSqlRaw`. It appears when - and only when - something is composed on
    // top and EF has to build a SELECT around the caller's text.
    [ConditionalFact]
    public async Task A_composed_FromSqlRaw_is_wrapped_in_a_subquery()
    {
        using SqliteConnection connection = OpenProbeDatabase();
        await using ProbeContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        const string Sql = @"SELECT ""Id"", ""Title"" FROM ""Rows""";

        string generated = context.Rows
            .FromSqlRaw(Sql)
            .Where(r => r.Id == 1)
            .ToQueryString()
            .ReplaceLineEndings("\n");

        // EF re-indents the caller's text inside the subquery, so this asserts the shape rather
        // than a byte-for-byte nesting: a SELECT of its own over an aliased `FROM (...)`, with the
        // composed predicate outside it.
        Assert.StartsWith("SELECT ", generated);
        Assert.Contains("FROM (\n", generated);
        Assert.Contains(@"FROM ""Rows""", generated);
        Assert.Contains("WHERE", generated);
        Assert.NotEqual(Sql, generated.Trim());
    }

    // An in-memory database held open by one connection, so this probe touches no file. The Tier B
    // store sweeps `*.db*` out of the working directory once per process at a moment this class
    // does not control, and a probe that owned a file would be racing it.
    private static SqliteConnection OpenProbeDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection, string table)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static ProbeContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<ProbeContext>().UseSqlite(connection).Options);

    // A plain EF Core SQLite context. Deliberately not `SqliteSmokeContext`: this probe is about
    // what EF and the driver do, and reusing the shared model would tie it to that model's churn.
    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options)
    {
        public DbSet<ProbeRow> Rows => Set<ProbeRow>();
    }

    private sealed class ProbeRow
    {
        public int Id { get; set; }

        public string? Title { get; set; }
    }
}
