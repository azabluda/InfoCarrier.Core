// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>OverzealousInitializationTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     A model whose constructors eagerly populate their own navigations. That is exactly what
///     `ClearPlaceholderReferencesBlockingFixup` exists for (L6): a constructor-set placeholder
///     blocks EF's fixup, and it must be cleared only where a real principal would replace it.
/// </remarks>
public class OverzealousInitializationInfoCarrierTest(OverzealousInitializationInfoCarrierTest.InfoCarrierFixture fixture)
    : OverzealousInitializationTestBase<OverzealousInitializationInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : OverzealousInitializationFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "OverzealousInitializationInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
