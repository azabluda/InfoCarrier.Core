// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <c>IncludeOneToOneTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     `Include` over a one-to-one, where the dependent shares its principal's key. That is the
///     shape L27's from-query tracking changed most — a reference navigation is fixed up from the
///     principal side and from the dependent side at once — and nothing adopted covers it
///     directly.
/// </remarks>
public class IncludeOneToOneInfoCarrierTest(IncludeOneToOneInfoCarrierTest.InfoCarrierFixture fixture)
    : IncludeOneToOneTestBase<IncludeOneToOneInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : OneToOneQueryFixtureBase
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
