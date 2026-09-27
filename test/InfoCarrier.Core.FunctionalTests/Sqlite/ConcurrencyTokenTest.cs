// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    private static SqliteInfoCarrierBackendTestStore CreateStore(Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? onAddOptions = null)
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(ConcurrencyContext),
                OnModelCreating = (_, _) => { },
                OnAddOptions = onAddOptions,
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

    /// <summary>
    ///     The same stale write, where the token that changed is a member of a complex property.
    /// </summary>
    /// <remarks>
    ///     EF's <c>OptimisticConcurrencySqliteTest.Property_entry_original_value_is_set</c> expects
    ///     <c>WHERE … "StorageLocation_Latitude" = @p4 AND "StorageLocation_Longitude" = @p5</c>, and
    ///     the server's <c>UPDATE</c> for that test checked only the scalar tokens: comparing its SQL
    ///     with EF's, 2026-09-15. A write that ignores a changed token overwrites someone else's row.
    /// </remarks>
    [ConditionalFact]
    public async Task A_stale_write_is_refused_when_the_token_is_in_a_complex_property()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Crate { Id = 1, Name = "original", Place = new Place { Latitude = 1, Longitude = 2 } });
                await context.SaveChangesAsync();
            });

        await using ConcurrencyContext client = CreateClient(store);
        Crate crate = await client.Crates.SingleAsync();

        await using (DbContext other = store.CreateDbContext())
        {
            Crate theirs = await other.Set<Crate>().SingleAsync();
            theirs.Place.Latitude = 99;
            await other.SaveChangesAsync();
        }

        crate.Name = "mine";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => client.SaveChangesAsync());
    }

    /// <summary>
    ///     The same stale write, where the token that changed is a member of an owned reference
    ///     that shares the owner's table.
    /// </summary>
    /// <remarks>
    ///     This is the shape of EF's <c>Engine.StorageLocation</c>, which is <c>OwnsOne</c> and not a
    ///     complex property, and the one whose tokens were missing from the server's <c>UPDATE</c>.
    ///     EF checks an unchanged owned entry's tokens when the row it shares is written.
    /// </remarks>
    [ConditionalFact]
    public async Task A_stale_write_is_refused_when_the_token_is_in_an_owned_reference()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Parcel { Id = 1, Name = "original", Spot = new Spot { Latitude = 1, Longitude = 2 } });
                await context.SaveChangesAsync();
            });

        await using ConcurrencyContext client = CreateClient(store);
        Parcel parcel = await client.Parcels.SingleAsync();

        await using (DbContext other = store.CreateDbContext())
        {
            Parcel theirs = await other.Set<Parcel>().SingleAsync();
            theirs.Spot.Latitude = 99;
            await other.SaveChangesAsync();
        }

        parcel.Name = "mine";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => client.SaveChangesAsync());
    }

    /// <summary>
    ///     An application configured correctly, with the suppressing interceptor on the SERVER:
    ///     the rest of the save is written, as plain EF Core writes it.
    /// </summary>
    /// <remarks>
    ///     The configuration <c>website/docs/limitations.md</c> recommends, and the owner's
    ///     (2026-09-27). Plain EF Core on the same store, measured on 2026-09-27: <c>SaveChanges</c>
    ///     returns 2, the insert is written, and the stale update is not.
    /// </remarks>
    [ConditionalFact]
    public async Task A_suppression_configured_on_the_server_writes_the_rest_of_the_save()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore(b => b.AddInterceptors(new ConflictSuppressor()));
        await SeedAsync(store);

        await using ConcurrencyContext client = CreateClient(store);
        (int saved, string stored) = await SaveAStaleUpdateAndAnInsertAsync(store, client);

        Assert.Equal(2, saved);
        Assert.Equal("1:original:99, 2:new:1", stored);
    }

    /// <summary>
    ///     An application MISCONFIGURED, with the suppressing interceptor on the client: nothing of
    ///     the save is written, and the save reports no error.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         What <c>website/docs/limitations.md</c> tells a reader they would observe, pinned here
    ///         for that page and so that the page changes when the behaviour does. The server's EF has
    ///         thrown and rolled the save back by the time the client's interceptor is asked, so the
    ///         insert beside the conflict is lost, and EF then accepts every change in the client's
    ///         change tracker.
    ///     </para>
    ///     <para>
    ///         The owner, 2026-09-27: documented as a limitation, with the interceptor on the server
    ///         as the correct configuration. Resending the rest of the save, or refusing the
    ///         suppression, would each change this test.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_suppression_misconfigured_on_the_client_writes_nothing()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);

        await using var client = new ConcurrencyContext(
            new DbContextOptionsBuilder<ConcurrencyContext>().UseInfoCarrier(store).AddInterceptors(new ConflictSuppressor()).Options);
        (int saved, string stored) = await SaveAStaleUpdateAndAnInsertAsync(store, client);

        Assert.Equal(0, saved);
        Assert.Equal("1:original:99", stored);
        Assert.All(client.ChangeTracker.Entries(), e => Assert.Equal(EntityState.Unchanged, e.State));
    }

    /// <summary>
    ///     Loads the seeded widget, lets another context change its token, then saves a change to it
    ///     together with a new widget, and reads back what the store holds.
    /// </summary>
    private static async Task<(int Saved, string Stored)> SaveAStaleUpdateAndAnInsertAsync(
        SqliteInfoCarrierBackendTestStore store, ConcurrencyContext client)
    {
        Widget widget = await client.Widgets.SingleAsync();

        await using (DbContext other = store.CreateDbContext())
        {
            Widget theirs = await other.Set<Widget>().SingleAsync();
            theirs.Version = 99;
            await other.SaveChangesAsync();
        }

        widget.Name = "mine";
        client.Add(new Widget { Id = 2, Name = "new", Version = 1 });

        int saved = await client.SaveChangesAsync();

        await using DbContext check = store.CreateDbContext();
        List<Widget> rows = await check.Set<Widget>().OrderBy(w => w.Id).ToListAsync();
        return (saved, string.Join(", ", rows.Select(w => $"{w.Id}:{w.Name}:{w.Version}")));
    }

    private sealed class ConflictSuppressor : SaveChangesInterceptor
    {
        public override InterceptionResult ThrowingConcurrencyException(
            ConcurrencyExceptionEventData eventData,
            InterceptionResult result)
            => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(
            ConcurrencyExceptionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }
}
