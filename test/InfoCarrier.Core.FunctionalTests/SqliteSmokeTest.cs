// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     The relational tier's vertical slice (ADR-009 Tier B): a client context with no database
///     queries through the in-process transport against a server context on **SQLite**.
/// </summary>
/// <remarks>
///     Tier A proves the pipeline; this proves it against a provider that actually translates.
///     EF's InMemory provider client-evaluates nearly everything, so it cannot distinguish a
///     query this provider gets wrong from one InMemory simply cannot do — and it cannot test
///     transactions at all, since it raises <c>TransactionIgnoredWarning</c> as an error.
/// </remarks>
public class SqliteSmokeTest
{
    private static SqliteInfoCarrierBackendTestStore CreateStore()
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SmokeContext),

                // The base store hands this straight to TestModelSource, which does not accept
                // null; SmokeContext needs no customization beyond its own OnModelCreating.
                OnModelCreating = (_, _) => { },
            });

    private static SmokeContext CreateClient(SqliteInfoCarrierBackendTestStore store)
        => new(new DbContextOptionsBuilder<SmokeContext>().UseInfoCarrier(store).Options);

    [ConditionalFact]
    public async Task Client_query_round_trips_through_a_relational_server()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(
                    new Blog { Id = 1, Title = "alpha" },
                    new Blog { Id = 2, Title = "beta" });
                await context.SaveChangesAsync();
            });

        await using SmokeContext client = CreateClient(store);
        List<Blog> blogs = await client.Blogs.OrderBy(b => b.Id).ToListAsync();

        Assert.Equal(["alpha", "beta"], blogs.Select(b => b.Title));
    }

    [ConditionalFact]
    public async Task A_projection_is_split_and_answered_against_a_relational_server()
    {
        // The projection split's own claim, checked where it matters: the server translates a
        // real query and returns only the projected column, and the client rebuilds its own type.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(
                    new Blog { Id = 1, Title = "alpha" },
                    new Blog { Id = 2, Title = "beta" });
                await context.SaveChangesAsync();
            });

        await using SmokeContext client = CreateClient(store);
        var rows = await client.Blogs
            .Where(b => b.Id > 1)
            .Select(b => new { b.Title, Length = b.Title!.Length })
            .ToListAsync();

        Assert.Equal("beta", Assert.Single(rows).Title);
        Assert.Equal(4, rows[0].Length);
    }

    [ConditionalFact]
    public async Task The_store_keeps_one_connection_open_for_its_lifetime()
    {
        // An in-memory SQLite database is destroyed when its last connection closes. If the
        // store let EF open and close per context, the schema and seed would vanish between
        // operations — so this asserts the ADR-009 requirement directly rather than trusting it.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Blog { Id = 1, Title = "alpha" });
                await context.SaveChangesAsync();
            });

        // A second, independent server context must still see the seeded row.
        using DbContext second = store.CreateDbContext();
        Assert.Equal(1, await second.Set<Blog>().CountAsync());
    }
}
