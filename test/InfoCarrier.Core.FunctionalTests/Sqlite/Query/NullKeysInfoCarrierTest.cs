// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NullKeysTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         A model whose foreign keys are null, which is the case every identity path here has to
///         answer "no principal" for rather than build a key from. The client resolves identity by
///         key array and the server decides what a navigation is loaded from, so a null key is a
///         branch on both sides of the wire.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. Base and fixture follow
///         EF's own SQLite class.
///     </para>
/// </remarks>
public class NullKeysInfoCarrierTest(NullKeysInfoCarrierTest.InfoCarrierFixture fixture)
    : NullKeysTestBase<NullKeysInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : NullKeysFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
