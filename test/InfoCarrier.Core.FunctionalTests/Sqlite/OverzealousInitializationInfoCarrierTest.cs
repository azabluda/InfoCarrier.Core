// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>OverzealousInitializationTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         A model whose constructors eagerly populate their own navigations. That is exactly what
///         `ClearPlaceholderReferencesBlockingFixup` exists for (L6): a constructor-set placeholder
///         blocks EF's fixup, and it must be cleared only where a real principal would replace it.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement.
///     </para>
/// </remarks>
public class OverzealousInitializationInfoCarrierTest(OverzealousInitializationInfoCarrierTest.InfoCarrierFixture fixture)
    : OverzealousInitializationTestBase<OverzealousInitializationInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : OverzealousInitializationFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "OverzealousInitializationInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
