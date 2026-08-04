// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>MusicStoreTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     An application-shaped suite rather than a feature-shaped one: a shopping cart, an order and
///     a catalogue, exercised the way a controller would. That is the point of adopting it — it
///     mixes query, tracking and <c>SaveChanges</c> in one context per operation, which is the
///     combination a per-feature base never quite reaches.
///     <para>
///         The transaction shim is EF's own <c>MusicStoreInMemoryTest</c>'s: the InMemory store has
///         no transaction to roll back, so a test that expects one clears the store instead.
///     </para>
/// </remarks>
public class MusicStoreInfoCarrierTest(MusicStoreInfoCarrierTest.MusicStoreInfoCarrierFixture fixture)
    : MusicStoreTestBase<MusicStoreInfoCarrierTest.MusicStoreInfoCarrierFixture>(fixture)
{
    public class MusicStoreInfoCarrierFixture : MusicStoreFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "MusicStoreInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <summary>
        ///     Ends a "transaction" by emptying the store, as EF's InMemory fixture does — but
        ///     through the <em>backend</em>.
        /// </summary>
        /// <remarks>
        ///     EF writes <c>context.Database.EnsureDeleted()</c>, and on this provider that is the
        ///     <em>client</em> context, which has no database: it deletes nothing and returns.
        ///     Every test's cart therefore survived into the next one and the counts accumulated
        ///     (A59 classified five failures that way). The backend owns the store, so it is the
        ///     backend that has to be emptied — the same rule A74 found for reseeding.
        /// </remarks>
        public override IDisposable BeginTransaction(DbContext context)
            => new BackendCleaner(((InfoCarrierTestStore)TestStore).Backend);

        private sealed class BackendCleaner(InfoCarrierBackendTestStore backend) : IDisposable
        {
            public void Dispose()
            {
                using DbContext context = backend.CreateDbContext();
                backend.CleanAsync(context).GetAwaiter().GetResult();
            }
        }
    }
}
