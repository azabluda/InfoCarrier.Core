// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <c>PrimitiveCollectionsQueryTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     The shared-model half of the primitive-collection corpus: one entity carrying an
///     <c>int[]</c>, a <c>List&lt;string&gt;</c>, a <c>DateTime[]</c> and their nullable twins,
///     queried through every operator a collection supports. <b>Tier B</b> for the same reason as
///     <see cref="NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest" /> — EF ships
///     <c>PrimitiveCollectionsQuerySqliteTest</c> and no InMemory counterpart, because a primitive
///     collection is a thing a store either maps or does not.
/// </remarks>
public class PrimitiveCollectionsQuerySqliteInfoCarrierTest(
    PrimitiveCollectionsQuerySqliteInfoCarrierTest.PrimitiveCollectionsQuerySqliteInfoCarrierFixture fixture)
    : PrimitiveCollectionsQueryTestBase<
        PrimitiveCollectionsQuerySqliteInfoCarrierTest.PrimitiveCollectionsQuerySqliteInfoCarrierFixture>(fixture)
{
    public class PrimitiveCollectionsQuerySqliteInfoCarrierFixture : PrimitiveCollectionsQueryFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "PrimitiveCollectionsQuerySqliteInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context));
    }
}
