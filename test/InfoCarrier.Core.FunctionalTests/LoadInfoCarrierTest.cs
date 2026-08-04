// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>LoadTestBase</c> on ADR-009 Tier A, mirroring EF's own <c>LoadInMemoryTest</c>.
/// </summary>
/// <remarks>
///     Explicit loading — <c>Entry(e).Collection(...).LoadAsync()</c> and friends — is a query
///     built from a <em>tracked</em> entity's key and fixed up into a graph the client already
///     holds. Nothing before this exercised that path.
/// </remarks>
public class LoadInfoCarrierTest(LoadInfoCarrierTest.InfoCarrierFixture fixture)
    : LoadTestBase<LoadInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : LoadFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "LoadInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
