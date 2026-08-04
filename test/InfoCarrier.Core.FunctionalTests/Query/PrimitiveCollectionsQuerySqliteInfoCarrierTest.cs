// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

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
    /// <summary>
    ///     The thirteen overrides below are EF's own, from <c>PrimitiveCollectionsQuerySqliteTest</c>.
    /// </summary>
    /// <remarks>
    ///     Each is a query that now reaches SQL and asks SQLite for <c>APPLY</c>, which it does not
    ///     have. That is convergence with the reference provider, not a defect of this one — the
    ///     rule CLAUDE.md states for a newly-red SQLite test. EF overrides a fourteenth,
    ///     <c>Project_collection_of_nullable_ints_with_distinct</c>, which is skipped here.
    /// </remarks>
    public override async Task Column_collection_SelectMany()
        => await AssertApplyNotSupported(() => base.Column_collection_SelectMany());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Column_collection_SelectMany_with_filter()
        => await AssertApplyNotSupported(() => base.Column_collection_SelectMany_with_filter());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Column_collection_SelectMany_with_Select_to_anonymous_type()
        => await AssertApplyNotSupported(() => base.Column_collection_SelectMany_with_Select_to_anonymous_type());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_datetimes_filtered()
        => await AssertApplyNotSupported(() => base.Project_collection_of_datetimes_filtered());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_ints_ordered()
        => await AssertApplyNotSupported(() => base.Project_collection_of_ints_ordered());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_ints_with_ToList_and_FirstOrDefault()
        => await AssertApplyNotSupported(() => base.Project_collection_of_ints_with_ToList_and_FirstOrDefault());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_ints_with_distinct()
        => await AssertApplyNotSupported(() => base.Project_collection_of_ints_with_distinct());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_nullable_ints_with_paging()
        => await AssertApplyNotSupported(() => base.Project_collection_of_nullable_ints_with_paging());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_nullable_ints_with_paging2()
        => await AssertApplyNotSupported(() => base.Project_collection_of_nullable_ints_with_paging2());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_nullable_ints_with_paging3()
        => await AssertApplyNotSupported(() => base.Project_collection_of_nullable_ints_with_paging3());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_empty_collection_of_nullables_and_collection_only_containing_nulls()
        => await AssertApplyNotSupported(
            () => base.Project_empty_collection_of_nullables_and_collection_only_containing_nulls());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_inline_collection_with_Union()
        => await AssertApplyNotSupported(() => base.Project_inline_collection_with_Union());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_multiple_collections()
        => await AssertApplyNotSupported(() => base.Project_multiple_collections());

    private static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);

    public class PrimitiveCollectionsQuerySqliteInfoCarrierFixture : PrimitiveCollectionsQueryFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "PrimitiveCollectionsQuerySqliteInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
