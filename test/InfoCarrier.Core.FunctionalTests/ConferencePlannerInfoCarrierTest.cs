// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>ConferencePlannerTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     The second application-shaped suite after <c>MusicStore</c>, and the more useful of the two
///     here: every test is a controller action — load, project into a DTO, mutate, save — run
///     against a context per operation. That is the combination a per-feature base never quite
///     reaches, and it is the shape a real caller of this provider writes.
/// </remarks>
public class ConferencePlannerInfoCarrierTest(ConferencePlannerInfoCarrierTest.ConferencePlannerInfoCarrierFixture fixture)
    : ConferencePlannerTestBase<ConferencePlannerInfoCarrierTest.ConferencePlannerInfoCarrierFixture>(fixture)
{
    public class ConferencePlannerInfoCarrierFixture : ConferencePlannerFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ConferencePlannerInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context));
    }
}
