// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     What the server runs for one call of the client, statement by statement.
/// </summary>
/// <remarks>
///     <para>
///         <b>Each test is a promise about this provider, and the statement is the evidence.</b> The
///         name says the promise, the body is the call, and the expected text is what the server may
///         run for it. A red test here says which promise broke, not which scenario changed.
///     </para>
///     <para>
///         <b>The last region holds something else</b>: a difference from EF that was read and
///         accepted. It is pinned for the same reason, and kept apart so that the promises above read
///         as one list.
///     </para>
///     <para>
///         <b>The model, the query and the expected text are all ours.</b> Comparing with EF's own
///         provider tests is how several of these were found (<c>eng/ef-sql-diff.py</c>, 2026-09-16),
///         and that comparison is an investigation, not a gate: it reports, we read it, and what we
///         learn ends here in our own words. Nothing in this file goes stale when EF ships a version.
///     </para>
///     <para>
///         <b>Names are ignored and structure is not.</b> <see cref="ServerSqlAssertions" /> compares
///         whitespace, parameter names and column aliases loosely, because a parameter crosses inside
///         a <c>ParameterBox&lt;T&gt;</c> and the projection split renames a column. A literal where a
///         parameter belongs, a missing <c>LIMIT</c>, an extra statement or a column in a <c>SET</c>
///         list that nobody changed are all differences this sees.
///     </para>
///     <para>
///         <b>Where a promise is a relation rather than a construct, it belongs in
///         <see cref="ServerParameterizationTest" /> instead</b>, which compares this client's
///         statement with the same query run directly against the server and needs no text at all.
///     </para>
/// </remarks>
public class ServerSqlTest
{
    #region Queries

    /// <summary>The filter is the server's work, and no row that fails it crosses the wire.</summary>
    [ConditionalFact]
    public Task A_filter_runs_on_the_server()
        => AssertServerRuns(
            client => client.Tickets.Where(t => t.Priority > 1).ToListAsync(),
            """
SELECT "t"."Id", "t"."Priority", "t"."Subject", "t"."Markers", "t"."Venue_Hall", "t"."Venue_Row", "t"."Holder_Badge", "t"."Holder_Name"
FROM "Tickets" AS "t"
WHERE "t"."Priority" > 1
""");

    /// <summary>A projection asks the store for the columns it uses and no others.</summary>
    [ConditionalFact]
    public Task A_projection_asks_only_for_the_columns_it_uses()
        => AssertServerRuns(
            client => client.Tickets.Select(t => t.Subject).ToListAsync(),
            """
SELECT "t"."Subject"
FROM "Tickets" AS "t"
""");

    /// <summary>
    ///     A terminal operator above a projection this client reassembles still bounds the rows at
    ///     the server.
    /// </summary>
    /// <remarks>
    ///     <b>This is the defect found on 2026-09-16.</b> The operator cannot ship, because it
    ///     consumes rows the client puts back together, and without its limit going with the shipped
    ///     query the server returned every matching row and the client kept one. The answer was
    ///     right and the whole table crossed the wire.
    /// </remarks>
    [ConditionalFact]
    public Task A_First_over_a_client_projection_sends_its_row_limit_to_the_server()
        => AssertServerRuns(
            client => client.Tickets.Where(t => t.Priority > 0).Select(t => new { t.Id, t.Subject }).FirstAsync(),
            """
SELECT "t"."Id" AS "Item1", "t"."Subject" AS "Item2"
FROM "Tickets" AS "t"
WHERE "t"."Priority" > 0
LIMIT 1
""");

    /// <summary>
    ///     <c>Single</c> sends a limit of two: one row cannot show that a second exists.
    /// </summary>
    /// <inheritdoc cref="A_First_over_a_client_projection_sends_its_row_limit_to_the_server" />
    [ConditionalFact]
    public Task A_Single_over_a_client_projection_sends_a_limit_of_two_to_the_server()
        => AssertServerRuns(
            client => client.Tickets.Where(t => t.Id == 1).Select(t => new { t.Id, t.Subject }).SingleAsync(),
            """
SELECT "t"."Id" AS "Item1", "t"."Subject" AS "Item2"
FROM "Tickets" AS "t"
WHERE "t"."Id" = 1
LIMIT 2
""");

    /// <summary>Paging is the store's work: the client never receives the pages it skipped.</summary>
    [ConditionalFact]
    public Task Paging_sends_the_limit_and_the_offset_to_the_server()
        => AssertServerRuns(
            client => client.Tickets.OrderBy(t => t.Id).Skip(1).Take(1).ToListAsync(),
            """
SELECT "t"."Id", "t"."Priority", "t"."Subject", "t"."Markers", "t"."Venue_Hall", "t"."Venue_Row", "t"."Holder_Badge", "t"."Holder_Name"
FROM "Tickets" AS "t"
ORDER BY "t"."Id"
LIMIT @Value0 OFFSET @Value
""");

    /// <summary>An <c>Include</c> is one statement with a join, not one query per parent.</summary>
    [ConditionalFact]
    public Task An_Include_runs_as_one_statement_with_a_join()
        => AssertServerRuns(
            client => client.Tickets.Include(t => t.Seats).OrderBy(t => t.Id).ToListAsync(),
            """
SELECT "t"."Id", "t"."Priority", "t"."Subject", "t"."Markers", "t"."Venue_Hall", "t"."Venue_Row", "t"."Holder_Badge", "t"."Holder_Name", "s"."Id", "s"."Label", "s"."TicketId"
FROM "Tickets" AS "t"
LEFT JOIN "Seats" AS "s" ON "t"."Id" = "s"."TicketId"
ORDER BY "t"."Id"
""");

    /// <summary>
    ///     A join on a composite key is one statement on the server, and it keeps C# null matching.
    /// </summary>
    /// <remarks>
    ///     <b>This is the defect found on 2026-09-16.</b> A composite key is written
    ///     <c>new { … }</c>, whose type the caller's compiler generates, so the boundary could not
    ///     ship it and cut below the join: the server read both tables whole and this client joined
    ///     them. The answer was right and the wire carried everything.
    ///     <c>JoinKeyRewriter</c> gives the key a type the server has.
    /// </remarks>
    [ConditionalFact]
    public Task A_join_on_a_composite_key_runs_one_statement_on_the_server()
        => AssertServerRuns(
            client => (from t in client.Tickets
                       join s in client.Seats on new { Id = t.Id, Name = t.Subject } equals new { Id = s.TicketId, Name = s.Label }
                       select new { t.Id, s.Label }).ToListAsync(),
            """
SELECT "t"."Id" AS "Item1", "s"."Label" AS "Item2"
FROM "Tickets" AS "t"
INNER JOIN "Seats" AS "s" ON "t"."Id" = "s"."TicketId" AND ("t"."Subject" = "s"."Label" OR ("t"."Subject" IS NULL AND "s"."Label" IS NULL))
""");

    /// <summary>
    ///     A split query stays split: two statements cross the wire, not one join and not three.
    /// </summary>
    [ConditionalFact]
    public Task A_split_query_runs_as_two_statements()
        => AssertServerRuns(
            client => client.Tickets.Include(t => t.Seats).AsSplitQuery().OrderBy(t => t.Id).ToListAsync(),
            """
SELECT "t"."Id", "t"."Priority", "t"."Subject", "t"."Markers", "t"."Venue_Hall", "t"."Venue_Row", "t"."Holder_Badge", "t"."Holder_Name"
FROM "Tickets" AS "t"
ORDER BY "t"."Id"
""",
            """
SELECT "s"."Id", "s"."Label", "s"."TicketId", "t"."Id"
FROM "Tickets" AS "t"
INNER JOIN "Seats" AS "s" ON "t"."Id" = "s"."TicketId"
ORDER BY "t"."Id"
""");

    /// <summary>An aggregate is computed by the store, and no row crosses the wire for it.</summary>
    [ConditionalFact]
    public Task A_Count_is_computed_by_the_store()
        => AssertServerRuns(
            client => client.Tickets.Where(t => t.Priority > 0).CountAsync(),
            """
SELECT COUNT(*)
FROM "Tickets" AS "t"
WHERE "t"."Priority" > 0
""");

    /// <summary>An existence check fetches no rows.</summary>
    [ConditionalFact]
    public Task An_Any_with_a_predicate_fetches_no_rows()
        => AssertServerRuns(
            client => client.Tickets.AnyAsync(t => t.Subject == "second"),
            """
SELECT EXISTS (
    SELECT 1
    FROM "Tickets" AS "t"
    WHERE "t"."Subject" = 'second')
""");

    /// <summary>A grouping is aggregated by the store, not by the client over every row.</summary>
    [ConditionalFact]
    public Task A_GroupBy_is_aggregated_by_the_store()
        => AssertServerRuns(
            client => client.Tickets.GroupBy(t => t.Priority).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(),
            """
SELECT "t"."Priority" AS "Item1", COUNT(*) AS "Item2"
FROM "Tickets" AS "t"
GROUP BY "t"."Priority"
""");

    /// <summary>
    ///     A collection of captured values reaches the store as parameters, not as its values.
    /// </summary>
    /// <remarks>
    ///     <b>This is the defect found on 2026-09-15.</b> Each element crossed the wire as a plain
    ///     constant, so the store parsed a new statement for every pair of values, and the plan cache
    ///     filled with statements that could never be reused.
    /// </remarks>
    [ConditionalFact]
    public async Task A_collection_of_captured_values_reaches_the_store_as_parameters()
    {
        int first = 1;
        int second = 2;

        await AssertServerRuns(
            client => client.Tickets.Where(t => new[] { first, second }.Contains(t.Id)).ToListAsync(),
            """
SELECT "t"."Id", "t"."Priority", "t"."Subject", "t"."Markers", "t"."Venue_Hall", "t"."Venue_Row", "t"."Holder_Badge", "t"."Holder_Name"
FROM "Tickets" AS "t"
WHERE "t"."Id" IN (@Value, @Value0)
""");
    }

    /// <summary>
    ///     A query the server cannot translate is refused, and no statement runs at all.
    /// </summary>
    /// <remarks>
    ///     The alternative a provider can fall into is fetching the table and filtering on the
    ///     client, which answers correctly and costs the whole table. This asserts the refusal by
    ///     asserting that the store was never asked anything.
    /// </remarks>
    [ConditionalFact]
    public async Task A_query_the_server_cannot_run_is_refused_and_runs_no_statement()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);
        await using ServerSqlContext client = CreateClient(store);
        store.ServerSql.Clear();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => client.Tickets.Where(t => NotTranslatable(t.Subject)).ToListAsync());

        store.AssertServerSql();
    }

    #endregion Queries

    #region Writes

    /// <summary>An update writes the property the client changed, and no other column.</summary>
    [ConditionalFact]
    public Task An_update_writes_only_the_property_the_client_changed()
        => AssertServerRunsAfter(
            client => client.Tickets.SingleAsync(t => t.Id == 1),
            async client =>
            {
                (await client.Tickets.FindAsync(1))!.Subject = "changed";
                await client.SaveChangesAsync();
            },
            """
UPDATE "Tickets" SET "Subject" = @p0
WHERE "Id" = @p1 AND "Venue_Hall" = @p2 AND "Holder_Badge" = @p3
RETURNING 1;
""");

    /// <summary>
    ///     A change to one member of a complex value writes that column alone.
    /// </summary>
    /// <remarks>
    ///     <b>This is the defect found on 2026-09-15.</b> The whole complex value was written, so a
    ///     member another client had changed between this client's read and its write was silently
    ///     overwritten with the value this client had loaded.
    /// </remarks>
    [ConditionalFact]
    public Task An_update_of_one_complex_member_writes_only_that_column()
        => AssertServerRunsAfter(
            client => client.Tickets.SingleAsync(t => t.Id == 1),
            async client =>
            {
                (await client.Tickets.FindAsync(1))!.Venue.Row = "B";
                await client.SaveChangesAsync();
            },
            """
UPDATE "Tickets" SET "Venue_Row" = @p0
WHERE "Id" = @p1 AND "Venue_Hall" = @p2 AND "Holder_Badge" = @p3
RETURNING 1;
""");

    /// <summary>
    ///     A complex collection in JSON that the client did not touch is not written.
    /// </summary>
    /// <inheritdoc cref="An_update_of_one_complex_member_writes_only_that_column" />
    [ConditionalFact]
    public Task An_update_leaves_a_json_collection_alone_when_the_client_did_not_change_it()
        => AssertServerRunsAfter(
            client => client.Tickets.SingleAsync(t => t.Id == 1),
            async client =>
            {
                (await client.Tickets.FindAsync(1))!.Priority = 9;
                await client.SaveChangesAsync();
            },
            """
UPDATE "Tickets" SET "Priority" = @p0
WHERE "Id" = @p1 AND "Venue_Hall" = @p2 AND "Holder_Badge" = @p3
RETURNING 1;
""");

    /// <summary>A change to the JSON collection writes that column, and nothing else.</summary>
    [ConditionalFact]
    public Task An_update_of_the_json_collection_writes_only_that_column()
        => AssertServerRunsAfter(
            client => client.Tickets.SingleAsync(t => t.Id == 1),
            async client =>
            {
                Ticket ticket = (await client.Tickets.FindAsync(1))!;
                ticket.Markers.Add(new Marker { Name = "late" });
                await client.SaveChangesAsync();
            },
            """
UPDATE "Tickets" SET "Markers" = @p0
WHERE "Id" = @p1 AND "Venue_Hall" = @p2 AND "Holder_Badge" = @p3
RETURNING 1;
""");

    /// <summary>
    ///     An update carries every concurrency token of the row, wherever the token is mapped.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>This is the defect found on 2026-09-15.</b> A token mapped from an OWNED reference
    ///         is an entry of its own in the change tracker, and an update of the principal sent that
    ///         entry to nobody: the server had no original value for the column, left it out of the
    ///         <c>WHERE</c>, and a write that should have been refused as stale succeeded. The other
    ///         client's change was lost with no error anywhere.
    ///     </para>
    ///     <para>
    ///         Both shapes are in this row on purpose: <c>Venue_Hall</c> comes from a complex value,
    ///         which travels inside the principal's own entry, and <c>Holder_Badge</c> from an owned
    ///         reference, which does not.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public Task An_update_carries_every_concurrency_token_of_the_row()
        => AssertServerRunsAfter(
            client => client.Tickets.SingleAsync(t => t.Id == 1),
            async client =>
            {
                (await client.Tickets.FindAsync(1))!.Subject = "token";
                await client.SaveChangesAsync();
            },
            """
UPDATE "Tickets" SET "Subject" = @p0
WHERE "Id" = @p1 AND "Venue_Hall" = @p2 AND "Holder_Badge" = @p3
RETURNING 1;
""");

    /// <summary>An insert names the columns the client set, and the store generates nothing extra.</summary>
    [ConditionalFact]
    public Task An_insert_names_the_columns_of_the_new_row()
        => AssertServerRunsAfter(
            _ => Task.CompletedTask,
            async client =>
            {
                client.Add(new Ticket
                {
                    Id = 3,
                    Subject = "third",
                    Priority = 3,
                    Venue = new Venue { Hall = "C", Row = "1" },
                    Holder = new Holder { Name = "cy", Badge = "z" },
                });
                await client.SaveChangesAsync();
            },
            """
INSERT INTO "Tickets" ("Markers", "Id", "Priority", "Subject", "Venue_Hall", "Venue_Row", "Holder_Badge", "Holder_Name")
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7);
""");

    /// <summary>A delete names the key and the concurrency token, so a stale delete is refused.</summary>
    [ConditionalFact]
    public Task A_delete_names_the_key_and_the_concurrency_token()
        => AssertServerRunsAfter(
            client => client.Tickets.SingleAsync(t => t.Id == 2),
            async client =>
            {
                client.Remove((await client.Tickets.FindAsync(2))!);
                await client.SaveChangesAsync();
            },
            """
DELETE FROM "Tickets"
WHERE "Id" = @p0 AND "Venue_Hall" = @p1 AND "Holder_Badge" = @p2
RETURNING 1;
""");

    /// <summary>
    ///     An <c>ExecuteUpdate</c> runs one statement, and reads nothing first.
    /// </summary>
    /// <remarks>
    ///     A provider that fetched the matching rows to update them one by one would answer the same
    ///     and cost a table scan plus a round trip per row.
    /// </remarks>
    [ConditionalFact]
    public Task An_ExecuteUpdate_runs_one_statement_and_reads_nothing()
        => AssertServerRuns(
            client => client.Tickets
                .Where(t => t.Priority > 1)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Subject, "bulk")),
            """
UPDATE "Tickets" AS "t"
SET "Subject" = @Value
WHERE "t"."Priority" > 1
""");

    /// <summary>An <c>ExecuteDelete</c> runs one statement, and reads nothing first.</summary>
    /// <inheritdoc cref="An_ExecuteUpdate_runs_one_statement_and_reads_nothing" />
    [ConditionalFact]
    public Task An_ExecuteDelete_runs_one_statement_and_reads_nothing()
        => AssertServerRuns(
            client => client.Seats.Where(s => s.Label == "A1").ExecuteDeleteAsync(),
            """
DELETE FROM "Seats" AS "s"
WHERE "s"."Label" = 'A1'
""");

    #endregion Writes

    #region Accepted deviations from EF

    // A difference from EF that was read, judged harmless and kept. It is pinned here for the
    // same reason a promise is: so that a change to it reports itself, rather than waiting to be
    // found by the next comparison run. What makes one acceptable is the owner's rule: no
    // dangerous SQL runs on the server and the visible behaviour is right.

    /// <summary>
    ///     A compiled query over a collection parameter ships the values it still needs, and not the
    ///     collection.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>A deviation from EF, accepted and pinned here (2026-09-17), and it is narrower than
    ///         it looks.</b> For an ORDINARY query EF folds the operator over a captured collection
    ///         itself and sends what is left as individual parameters, which is exactly what this
    ///         client sends — measured both ways, statement for statement. The difference is only
    ///         inside <c>EF.CompileQuery</c>: there EF keeps the collection symbolic, because that is
    ///         what compiling buys, and translates the operator into SQL over the parameter.
    ///     </para>
    ///     <para>
    ///         This client substitutes a compiled query's parameters before the boundary is computed
    ///         (ADR-006), so the operator folds here and the remaining values cross as parameters.
    ///         The answers agree, and the statement's shape varies with the collection's length
    ///         exactly as EF's own default does outside a compiled query, so the store's plan cache
    ///         is no worse off. <c>EF.Parameter</c> would give a constant shape through
    ///         <c>json_each</c>, and EF does not use it by default either.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_compiled_query_over_a_collection_parameter_ships_the_values_it_still_needs()
    {
        Func<ServerSqlContext, int[], Task<int>> compiled = EF.CompileAsyncQuery(
            (ServerSqlContext context, int[] ids) => context.Tickets.Count(t => ids.Skip(1).Contains(t.Id)));

        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);
        await using ServerSqlContext client = CreateClient(store);

        store.ServerSql.Clear();
        Assert.Equal(1, await compiled(client, [1, 2]));

        // One parameter, the value that is left after `Skip(1)`, and still a PARAMETER: the plan
        // cache sees one statement however the collection changes.
        store.AssertServerSql(
            """
SELECT COUNT(*)
FROM "Tickets" AS "t"
WHERE "t"."Id" = @Skip1
""");
    }

    #endregion Accepted deviations from EF

    private static bool NotTranslatable(string? subject)
        => subject?.Length > 3;

    private static SqliteInfoCarrierBackendTestStore CreateStore()
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(ServerSqlContext),
                OnModelCreating = (_, _) => { },
            });

    private static Task SeedAsync(SqliteInfoCarrierBackendTestStore store)
        => store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(
                    new Ticket
                    {
                        Id = 1,
                        Subject = "first",
                        Priority = 1,
                        Venue = new Venue { Hall = "A", Row = "1" },
                        Holder = new Holder { Name = "ann", Badge = "x" },
                        Markers = [new Marker { Name = "early" }],
                        Seats = [new Seat { Id = 1, Label = "A1" }],
                    },
                    new Ticket
                    {
                        Id = 2,
                        Subject = "second",
                        Priority = 2,
                        Venue = new Venue { Hall = "B", Row = "2" },
                        Holder = new Holder { Name = "bob", Badge = "y" },
                        Markers = [],
                        Seats = [new Seat { Id = 2, Label = "B1" }],
                    });
                await context.SaveChangesAsync();
            });

    private static ServerSqlContext CreateClient(SqliteInfoCarrierBackendTestStore store)
        => new(new DbContextOptionsBuilder<ServerSqlContext>().UseInfoCarrier(store).Options);

    /// <summary>Runs one call of the client, and asserts what the server ran for it.</summary>
    private static async Task AssertServerRuns(Func<ServerSqlContext, Task> act, params string[] expected)
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);
        await using ServerSqlContext client = CreateClient(store);

        store.ServerSql.Clear();
        await act(client);

        store.AssertServerSql(expected);
    }

    /// <summary>
    ///     The same, where the call needs something loaded first: the statements of
    ///     <paramref name="arrange" /> are forgotten, so the assertion is about the write alone.
    /// </summary>
    private static async Task AssertServerRunsAfter(
        Func<ServerSqlContext, Task> arrange,
        Func<ServerSqlContext, Task> act,
        params string[] expected)
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);
        await using ServerSqlContext client = CreateClient(store);

        await arrange(client);
        store.ServerSql.Clear();
        await act(client);

        store.AssertServerSql(expected);
    }
}
