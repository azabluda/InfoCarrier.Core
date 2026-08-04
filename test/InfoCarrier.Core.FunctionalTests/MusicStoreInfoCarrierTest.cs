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
                (modelBuilder, context) => OnModelCreating(modelBuilder, context));

        public override IDisposable BeginTransaction(DbContext context)
            => new InMemoryCleaner(context);

        private sealed class InMemoryCleaner(DbContext context) : IDisposable
        {
            public void Dispose()
                => context.Database.EnsureDeleted();
        }
    }
}
