// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>NullKeysTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     A model whose foreign keys are null, which is the case every identity path here has to
///     answer "no principal" for rather than build a key from. The client resolves identity by
///     key array and the server decides what a navigation is loaded from, so a null key is a
///     branch on both sides of the wire.
/// </remarks>
public class NullKeysInfoCarrierTest(NullKeysInfoCarrierTest.InfoCarrierFixture fixture)
    : NullKeysTestBase<NullKeysInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : NullKeysFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
