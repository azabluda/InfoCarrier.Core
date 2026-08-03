// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>ManyToManyFieldsLoadTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     The same skip-navigation loading as `ManyToManyLoad`, over a field-only model — the
///     intersection of the two things this batch is aimed at.
/// </remarks>
public class ManyToManyFieldsLoadInfoCarrierTest(ManyToManyFieldsLoadInfoCarrierTest.InfoCarrierFixture fixture)
    : ManyToManyFieldsLoadTestBase<ManyToManyFieldsLoadInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : ManyToManyFieldsLoadFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "ManyToManyFieldsLoadInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context));
    }
}
