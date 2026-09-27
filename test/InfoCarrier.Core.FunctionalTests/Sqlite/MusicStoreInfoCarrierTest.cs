// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>MusicStoreTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         An application-shaped suite rather than a feature-shaped one: a shopping cart, an order
///         and a catalogue, exercised the way a controller would. That is the point of adopting it
///         — it mixes query, tracking and <c>SaveChanges</c> in one context per operation, which is
///         the combination a per-feature base never quite reaches.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. Each test's transaction
///         is now a real one, held by the server, as in EF's own <c>MusicStoreSqliteTest</c>. On
///         Tier A the fixture emptied the store through the backend instead, because InMemory has
///         no transaction to roll back.
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
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
