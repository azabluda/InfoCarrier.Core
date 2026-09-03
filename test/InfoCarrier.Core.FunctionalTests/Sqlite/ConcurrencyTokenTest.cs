// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     Optimistic concurrency across the wire, on ADR-009 Tier B — the only tier that can show
///     it. EF's InMemory provider performs no concurrency check at all, which is why EF's own
///     <c>OptimisticConcurrencyInMemoryTest</c> skips sixteen of its tests.
/// </summary>
/// <remarks>
///     <para>
///         A concurrency check compares the row's <em>original</em> token against the store. The
///         server does not receive one: it rebuilds each entity from the current values, attaches
///         it and sets <c>Modified</c>, and an entry attached that way has
///         <c>OriginalValues == CurrentValues</c> by construction.
///         <c>SaveChangesRequest.SerializedOriginalValues</c> exists on the wire for this and has
///         never been written or read (plan S3c).
///     </para>
///     <para>
///         The two directions fail differently, which is why both are here: a client that leaves
///         the token alone happens to send the right original value and is checked correctly,
///         while a client that <em>bumps</em> the token — the whole point of an
///         application-managed one — sends the new value as its own original and is refused a
///         write nobody conflicted with.
///     </para>
/// </remarks>
public class ConcurrencyTokenTest
{
    private static SqliteInfoCarrierBackendTestStore CreateStore()
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(ConcurrencyContext),
                OnModelCreating = (_, _) => { },
            });

    private static ConcurrencyContext CreateClient(SqliteInfoCarrierBackendTestStore store)
        => new(new DbContextOptionsBuilder<ConcurrencyContext>().UseInfoCarrier(store).Options);

    private static Task SeedAsync(SqliteInfoCarrierBackendTestStore store)
        => store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Widget { Id = 1, Name = "original", Version = 1 });
                await context.SaveChangesAsync();
            });

    [ConditionalFact]
    public async Task A_client_that_bumps_the_concurrency_token_is_not_a_conflict()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);

        await using ConcurrencyContext client = CreateClient(store);
        Widget widget = await client.Widgets.SingleAsync();

        // The application-managed pattern: change the row and bump its token in one go. The
        // original token is still 1, which is what the store holds, so nothing conflicts.
        widget.Name = "updated";
        widget.Version = 2;

        await client.SaveChangesAsync();

        await using DbContext server = store.CreateDbContext();
        Widget stored = await server.Set<Widget>().SingleAsync();
        Assert.Equal("updated", stored.Name);
        Assert.Equal(2, stored.Version);
    }

    [ConditionalFact]
    public async Task A_stale_write_is_refused()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);

        await using ConcurrencyContext client = CreateClient(store);
        Widget widget = await client.Widgets.SingleAsync();

        // Someone else commits first, straight against the store.
        await using (DbContext other = store.CreateDbContext())
        {
            Widget theirs = await other.Set<Widget>().SingleAsync();
            theirs.Name = "theirs";
            theirs.Version = 99;
            await other.SaveChangesAsync();
        }

        widget.Name = "mine";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => client.SaveChangesAsync());
    }
}
