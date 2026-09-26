// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     Each statement the server runs is filed under the running test, with its outcome (#167,
///     step H1a).
/// </summary>
/// <remarks>
///     In the Sqlite namespace because every pin needs a server that runs SQL. The store is this
///     class's own, seeded once by its fixture, so the seeding is the fixture's and no test's.
/// </remarks>
public class CommandCaptureTest(CommandCaptureTest.Fixture fixture) : IClassFixture<CommandCaptureTest.Fixture>
{
    /// <summary>
    ///     A reader's count is EF's own <c>ReadCount</c>, which counts the last <c>Read()</c> as well,
    ///     the one that returns false.
    /// </summary>
    [ConditionalFact]
    public async Task A_query_of_three_rows_is_one_reader_of_four_reads()
    {
        await using ServerSqlContext client = CreateClient();

        _ = await client.Tickets.ToListAsync();

        CapturedCommand command = Assert.Single(CurrentTest.Value!.Commands);
        Assert.Equal(CapturedCommandKind.Reader, command.Kind);
        Assert.Equal(4, command.Count);
        Assert.False(command.Failed);
        Assert.StartsWith("SELECT ", command.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>First</c> reads its row and then the end of the result, directly and through the wire
    ///     alike: EF runs it as <c>LIMIT 1</c> read with single cardinality, and that reads once more
    ///     to see that nothing follows.
    /// </summary>
    /// <remarks>
    ///     The plan expected one read (2026-09-26), and plain EF on the server's own context shows two.
    ///     A count is EF's, so the two sides count alike.
    /// </remarks>
    [ConditionalFact]
    public async Task First_reads_its_row_and_the_end_directly_and_through_the_wire()
    {
        await using ServerSqlContext client = CreateClient();
        _ = await client.Tickets.OrderBy(t => t.Id).FirstAsync();

        await using (DbContext server = fixture.Store.CreateDbContext())
        {
            _ = await server.Set<Ticket>().OrderBy(t => t.Id).FirstAsync();
        }

        Assert.Equal(
            [(CapturedCommandKind.Reader, 2), (CapturedCommandKind.Reader, 2)],
            CurrentTest.Value!.Commands.Select(c => (c.Kind, c.Count ?? -1)));
    }

    /// <summary>A command that throws is recorded, and marked failed.</summary>
    [ConditionalFact]
    public async Task A_SaveChanges_that_violates_a_key_is_a_failed_command()
    {
        await using ServerSqlContext client = CreateClient();
        client.Tickets.Add(new Ticket { Id = 1, Subject = "duplicate" });

        _ = await Assert.ThrowsAnyAsync<DbUpdateException>(() => client.SaveChangesAsync());

        CapturedCommand command = Assert.Single(CurrentTest.Value!.Commands);
        Assert.True(command.Failed);
        Assert.StartsWith("INSERT ", command.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Once the test is closed, a statement is not the test's any more. <c>After</c> closes it,
    ///     which <see cref="CommandCaptureDisposeTest" /> shows end to end.
    /// </summary>
    [ConditionalFact]
    public async Task After_Close_a_statement_is_not_recorded()
    {
        await using ServerSqlContext client = CreateClient();
        fixture.Store.ServerSql.Clear();

        CurrentTest.Value!.Close();
        _ = await client.Tickets.ToListAsync();

        Assert.Single(fixture.Store.ServerSql.Statements);
        Assert.Empty(CurrentTest.Value!.Commands);
    }

    /// <summary>The fixture seeded through the same interceptor, and none of it is this test's.</summary>
    [ConditionalFact]
    public void No_command_is_recorded_while_the_class_fixture_seeds()
    {
        Assert.Null(fixture.CurrentTestWhileSeeding);
        Assert.NotEqual(0, fixture.StatementsWhileSeeding);
        Assert.Empty(CurrentTest.Value!.Commands);
    }

    private ServerSqlContext CreateClient()
        => new(new DbContextOptionsBuilder<ServerSqlContext>().UseInfoCarrier(fixture.Store).Options);

    /// <summary>A store of three tickets, seeded before any test of the class starts.</summary>
    public sealed class Fixture : IAsyncLifetime
    {
        public SqliteInfoCarrierBackendTestStore Store { get; } = new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(ServerSqlContext),
                OnModelCreating = (_, _) => { },
            });

        public CurrentTest? CurrentTestWhileSeeding { get; private set; }

        public int StatementsWhileSeeding { get; private set; }

        public async Task InitializeAsync()
        {
            CurrentTestWhileSeeding = CurrentTest.Value;
            await Store.InitializeAsync(
                Store.ServiceProvider,
                Store.CreateDbContext,
                seed: async context =>
                {
                    context.AddRange(
                        new Ticket { Id = 1, Subject = "first" },
                        new Ticket { Id = 2, Subject = "second" },
                        new Ticket { Id = 3, Subject = "third" });
                    await context.SaveChangesAsync();
                });
            StatementsWhileSeeding = Store.ServerSql.Statements.Count;
        }

        public async Task DisposeAsync()
            => await Store.DisposeAsync();
    }
}

/// <summary>
///     A statement a test class runs in its <c>DisposeAsync</c> is filed under no test, because
///     <see cref="CloseCurrentTestAttribute" /> closed the test in <c>After</c> (#167, step H1).
/// </summary>
/// <remarks>
///     The assertions run in <c>DisposeAsync</c> itself, whose exception xUnit v2 reports as the
///     test's failure, and the async flow there still names the test. So without the attribute's
///     <c>Close</c> the statement would be filed, and this test fails: shown on 2026-09-26 by
///     removing <c>[assembly: CloseCurrentTest]</c>.
/// </remarks>
public class CommandCaptureDisposeTest(CommandCaptureTest.Fixture fixture)
    : IClassFixture<CommandCaptureTest.Fixture>, IAsyncLifetime
{
    private CurrentTest? _test;

    [ConditionalFact]
    public void A_statement_in_the_test_class_DisposeAsync_is_filed_under_no_test()
        => _test = CurrentTest.Value;

    /// <inheritdoc />
    public Task InitializeAsync()
        => Task.CompletedTask;

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        Assert.NotNull(_test);
        Assert.Same(_test, CurrentTest.Value);

        await using var client = new ServerSqlContext(
            new DbContextOptionsBuilder<ServerSqlContext>().UseInfoCarrier(fixture.Store).Options);
        fixture.Store.ServerSql.Clear();

        _ = await client.Tickets.ToListAsync();

        Assert.Single(fixture.Store.ServerSql.Statements);
        Assert.Empty(_test.Commands);
    }
}
