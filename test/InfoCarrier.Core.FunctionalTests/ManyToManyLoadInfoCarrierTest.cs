// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>ManyToManyLoadTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     Explicit and lazy loading of skip navigations. `ManyToManyTracking` is green, but it
///     saves and re-reads; this base loads, which is the half L7–L11 and L20–L21 rebuilt.
/// </remarks>
public class ManyToManyLoadInfoCarrierTest(ManyToManyLoadInfoCarrierTest.InfoCarrierFixture fixture)
    : ManyToManyLoadTestBase<ManyToManyLoadInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : ManyToManyLoadFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "ManyToManyLoadInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
