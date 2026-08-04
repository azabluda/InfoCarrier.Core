// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>FieldsOnlyLoadTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     Explicit and lazy loading over a model with no properties at all — every navigation
///     and scalar is a field. The loading paths this provider rewrote in phase L all read
///     through backing fields; this is the model where there is nothing else to read.
/// </remarks>
public class FieldsOnlyLoadInfoCarrierTest(FieldsOnlyLoadInfoCarrierTest.InfoCarrierFixture fixture)
    : FieldsOnlyLoadTestBase<FieldsOnlyLoadInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : FieldsOnlyLoadFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "FieldsOnlyLoadInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
