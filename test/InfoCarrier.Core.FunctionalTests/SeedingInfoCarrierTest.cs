// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>SeedingTestBase</c>, on ADR-009 <b>Tier A</b> — and A65's blocker routed around rather
///     than removed.
/// </summary>
/// <remarks>
///     <para>
///         A65 filed this base as blocked: <c>SeedingContext</c> takes a <c>string testId</c> and
///         has no <c>DbContextOptions</c> constructor, so the backend cannot build the server's
///         copy of it by the usual route. That is true, and it is not the whole picture — the base
///         hands the <em>client</em> context construction to the derived class
///         (<c>CreateContextWithEmptyDatabase</c>), so only the <em>server</em> copy needs the
///         ordinary constructor, and <c>serverContextType</c> exists precisely to supply a
///         different type there. The two contexts share `OnModelCreating`, which is where the
///         `HasData` seed lives, so they agree on the thing that matters.
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
                typeof(SeedingInfoCarrierServerContext),
                onModelCreating: null,
                serverContextType: typeof(SeedingInfoCarrierServerContext))
            .GetOrCreate("SeedingInfoCarrierTest");

    /// <inheritdoc />
    protected override SeedingContext CreateContextWithEmptyDatabase(string testId)
        => new SeedingInfoCarrierContext(testId, ((InfoCarrierTestStore)TestStore).Backend);

    private class SeedingInfoCarrierContext(string testId, IInfoCarrierClient client) : SeedingContext(testId)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInfoCarrier(client);
    }

    /// <summary>
    ///     The server's copy: the same seeded model, reachable through the ordinary
    ///     <c>DbContextOptions</c> constructor the backend builds by.
    /// </summary>
    public class SeedingInfoCarrierServerContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Seed>().HasData(
                new Seed { Id = 321, Species = "Apple" },
                new Seed { Id = 322, Species = "Orange" });
    }
}
