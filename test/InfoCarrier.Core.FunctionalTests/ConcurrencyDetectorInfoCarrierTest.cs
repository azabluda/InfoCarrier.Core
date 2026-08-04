// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>ConcurrencyDetectorEnabledTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     Two uses of one context at once must be refused, and this provider has to enforce that
///     itself: the round trip is guarded, and so is each row the residual produces (Z1). Both are
///     ours rather than EF's, which is exactly why the base is worth inheriting.
/// </remarks>
public class ConcurrencyDetectorEnabledInfoCarrierTest(
    ConcurrencyDetectorEnabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture fixture)
    : ConcurrencyDetectorEnabledTestBase<
        ConcurrencyDetectorEnabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture>(fixture)
{
    public class ConcurrencyDetectorInfoCarrierFixture : ConcurrencyDetectorFixtureBase
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

/// <summary>
///     <c>ConcurrencyDetectorDisabledTestBase</c> on Tier A — the same model with the checks off.
/// </summary>
/// <remarks>
///     The fixture is EF's own <c>ConcurrencyDetectorDisabledInMemoryTest</c>'s, down to the
///     <c>EnableThreadSafetyChecks(false)</c> that replaces rather than extends the base options.
/// </remarks>
public class ConcurrencyDetectorDisabledInfoCarrierTest(
    ConcurrencyDetectorDisabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture fixture)
    : ConcurrencyDetectorDisabledTestBase<
        ConcurrencyDetectorDisabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture>(fixture)
{
    public class ConcurrencyDetectorInfoCarrierFixture : ConcurrencyDetectorFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => builder.EnableThreadSafetyChecks(enableChecks: false);
    }
}
