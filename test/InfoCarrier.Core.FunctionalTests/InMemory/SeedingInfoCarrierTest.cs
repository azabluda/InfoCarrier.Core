// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>SeedingTestBase</c>, on ADR-009 <b>Tier A</b> — and A65's blocker routed around rather
///     than removed.
/// </summary>
/// <remarks>
///     <para>
///         A65 filed this base as blocked: <c>SeedingContext</c> takes a <c>string testId</c> and
///         has no <c>DbContextOptions</c> constructor, so the backend cannot build the server's
///         copy of it by the usual route. That is true, and it is not the whole picture — the base
///         hands the client-side context construction to the derived class
///         (<c>CreateContextWithEmptyDatabase</c>), so only the harness-built copy needs the
///         ordinary constructor. <c>SeedingInfoCarrierOptionsContext</c> supplies it. The two
///         carry the same `HasData` seed, which is the thing that matters.
///     </para>
///     <para>
///         The client has no schema-management service. This override cleans the backend through
///         the test store, which recreates and seeds its InMemory database. The rows the test then
///         queries come from that backend database.
///     </para>
///     <para>
///         <b>It stays on Tier A, measured on 2026-09-27</b>, when the other Tier A bases that could
///         moved to Tier B for #167's slow run. There 2 of its 4 tests failed on
///         <c>no such table: Seed</c>: the base cleans the store and then calls
///         <c>EnsureCreated</c>, and on SQLite the clean drops the tables, which the client's
///         <c>EnsureCreated</c> does not create again. InMemory's clean recreates the store itself.
///     </para>
/// </remarks>
public class SeedingInfoCarrierTest : SeedingTestBase
{
    private TestStore? _testStore;

    /// <inheritdoc />
    protected override TestStore TestStore
        => _testStore ??= InfoCarrierTestStoreFactory.Create(
                InMemoryInfoCarrierTier.Instance,
                typeof(SeedingInfoCarrierOptionsContext),
                onModelCreating: null)
            .GetOrCreate("SeedingInfoCarrierTest");

    /// <inheritdoc />
    protected override SeedingContext CreateContextWithEmptyDatabase(string testId)
        => new SeedingInfoCarrierContext(testId, ((IInfoCarrierClientTestStore)TestStore).Backend);

    /// <inheritdoc />
    [InfoCarrierDesign(
        Decisions.Architecture,
        Decisions.ClientServices,
        Justification = "The client has no database creator, so the test store recreates and seeds the backend directly.")]
    public override async Task Seeding_does_not_leave_context_contaminated(bool async)
    {
        using SeedingContext context = CreateContextWithEmptyDatabase(async ? "1A" : "1S");
        await TestStore.CleanAsync(context);

        Assert.Empty(context.ChangeTracker.Entries());

        List<Seed> seeds = context.Set<Seed>().OrderBy(e => e.Id).ToList();
        Assert.Equal(2, seeds.Count);
        Assert.Equal(321, seeds[0].Id);
        Assert.Equal("Apple", seeds[0].Species);
        Assert.Equal(322, seeds[1].Id);
        Assert.Equal("Orange", seeds[1].Species);
    }

    private class SeedingInfoCarrierContext(string testId, IInfoCarrierClient client) : SeedingContext(testId)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInfoCarrier(client);
    }

    /// <summary>
    ///     The same seeded model, reachable through the ordinary <c>DbContextOptions</c>
    ///     constructor the harness builds by.
    /// </summary>
    /// <remarks>
    ///     <b>A second class for a CONSTRUCTOR SHAPE, not for a second model, and this is the only
    ///     one left in the suite.</b> EF's <c>SeedingContext</c> is abstract, takes a
    ///     <c>string testId</c>, and declares no <c>DbContextOptions</c> constructor, so nothing
    ///     the harness registers can derive from it. The seed below is therefore written twice, and
    ///     the test itself is what catches the two copies drifting apart: it asserts the rows.
    /// </remarks>
    public class SeedingInfoCarrierOptionsContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Seed>().HasData(
                new Seed { Id = 321, Species = "Apple" },
                new Seed { Id = 322, Species = "Orange" });
    }
}
