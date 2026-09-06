// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

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
///         Which is also why the client's <c>EnsureCreated</c> being a no-op is not a problem:
///         <c>InfoCarrierDatabaseCreator</c> reports success because the client has no store, and
///         the rows the test then queries for come from the <em>backend's</em> database, created
///         from the same seeded model.
///     </para>
/// </remarks>
public class SeedingInfoCarrierTest : SeedingTestBase
{
    private TestStore? _testStore;

    /// <inheritdoc />
    protected override TestStore TestStore
        => _testStore ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                typeof(SeedingInfoCarrierOptionsContext),
                onModelCreating: null)
            .GetOrCreate("SeedingInfoCarrierTest");

    /// <inheritdoc />
    protected override SeedingContext CreateContextWithEmptyDatabase(string testId)
        => new SeedingInfoCarrierContext(testId, ((IInfoCarrierClientTestStore)TestStore).Backend);

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
