// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>ComplexNavigationsSharedTypeQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         The complex-navigations corpus again, over a model whose levels are <em>shared-type</em>
///         entity types — the same navigation depth, but every entity type is keyed by name and
///         several share one CLR type. That is the case where reading a value by its CLR member is
///         not enough and the model has to be consulted, which is most of what this provider's
///         mapper does.
///     </para>
///     <para>
///         <b>Moved from Tier A under the Group C policy.</b> The relational fixture base maps all
///         four levels onto one table, so on a real database this is table splitting as well as
///         shared types — a shape InMemory could not express at all. Neither base declares
///         <c>UseTransaction</c> nor calls <c>ExecuteWithStrategyInTransactionAsync</c>.
///     </para>
/// </remarks>
public class ComplexNavigationsSharedTypeQueryInfoCarrierTest(ComplexNavigationsSharedTypeQueryInfoCarrierFixture fixture)
    : ComplexNavigationsSharedTypeQueryRelationalTestBase<ComplexNavigationsSharedTypeQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Nested_SelectMany_correlated_with_join_table_correctly_translated_to_apply(bool async)
        => AssertApplyNotSupported(() => base.Nested_SelectMany_correlated_with_join_table_correctly_translated_to_apply(async));

    /// <inheritdoc />
    public override Task Let_let_contains_from_outer_let(bool async)
        => AssertApplyNotSupported(() => base.Let_let_contains_from_outer_let(async));

    /// <inheritdoc />
    public override Task Prune_does_not_throw_null_ref(bool async)
        => AssertApplyNotSupported(() => base.Prune_does_not_throw_null_ref(async));

    /// <inheritdoc />
    public override Task Correlated_projection_with_first(bool async)
        => AssertApplyNotSupported(() => base.Correlated_projection_with_first(async));

    /// <inheritdoc />
    public override Task Multiple_select_many_in_projection(bool async)
        => AssertApplyNotSupported(() => base.Multiple_select_many_in_projection(async));

    /// <inheritdoc />
    public override Task Single_select_many_in_projection_with_take(bool async)
        => AssertApplyNotSupported(() => base.Single_select_many_in_projection_with_take(async));

    /// <inheritdoc />
    /// <remarks>
    ///     Client code in an <c>OrderBy</c> key decides the order of every row, so running it here
    ///     means fetching all of them. <c>ComplexNavigationsQuerySqliteTest</c> asserts the same
    ///     refusal with the same details clause; only the fixture named in the message differs.
    ///     The declaring type in the message is <c>ComplexNavigationsQueryTestBase</c>: the
    ///     shared-type base derives from it, and <c>ClientMethodNullableInt</c> is declared up
    ///     there.
    /// </remarks>
    public override Task GroupJoin_client_method_in_OrderBy(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.GroupJoin_client_method_in_OrderBy(async),
            CoreStrings.QueryUnableToTranslateMethod(
                "Microsoft.EntityFrameworkCore.Query.ComplexNavigationsQueryTestBase<"
                    + typeof(ComplexNavigationsSharedTypeQueryInfoCarrierFixture).FullName + ">",
                "ClientMethodNullableInt"));

    /// <inheritdoc />
    /// <remarks>
    ///     <b>Kept from Tier A, and it is the one place this class does not follow EF's SQLite
    ///     suite.</b> That suite expects <c>ApplyNotSupported</c>, because on SQLite the query
    ///     reaches the translator before anything rejects it. Here it never gets that far: C73
    ///     states the refusal on the query's result element type, which is what the test name asks
    ///     for, and the projection split raises it before the request crosses the wire.
    /// </remarks>
    public override Task Join_with_result_selector_returning_queryable_throws_validation_error(bool async)
        => AssertInvalidMaterializationType(
            () => base.Join_with_result_selector_returning_queryable_throws_validation_error(async),
            "IQueryable<Level3>");

    /// <summary>
    ///     The query reaches SQL and asks SQLite for <c>APPLY</c>, which it does not have. Every
    ///     use of this is a test EF's own <c>ComplexNavigations*QuerySqliteTest</c> overrides the
    ///     same way, so each one is convergence with the reference provider and not a workaround.
    /// </summary>
    internal static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);
}

/// <summary>
///     <c>ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase</c> on Tier B — the same
///     model, queried through its collections.
/// </summary>
/// <remarks>
///     The relational base adds one override of its own, for the join-key ambiguity a relational
///     provider reports; every override below is
///     <c>ComplexNavigationsCollectionsSharedTypeQuerySqliteTest</c>'s. As in the non-shared-type
///     sibling, <c>Projecting_collection_after_optional_reference_correlated_with_parent</c> is
///     deliberately absent, because it passes here.
/// </remarks>
public class ComplexNavigationsCollectionsSharedTypeQueryInfoCarrierTest(
    ComplexNavigationsSharedTypeQueryInfoCarrierFixture fixture)
    : ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase<
        ComplexNavigationsSharedTypeQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Complex_query_with_let_collection_projection_FirstOrDefault_with_ToList_on_inner_and_outer(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Complex_query_with_let_collection_projection_FirstOrDefault_with_ToList_on_inner_and_outer(async));

    /// <inheritdoc />
    public override Task Filtered_include_after_different_filtered_include_different_level(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_after_different_filtered_include_different_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_and_non_filtered_include_followed_by_then_include_on_same_navigation(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_and_non_filtered_include_followed_by_then_include_on_same_navigation(async));

    /// <inheritdoc />
    public override Task Filtered_include_complex_three_level_with_middle_having_filter1(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_complex_three_level_with_middle_having_filter1(async));

    /// <inheritdoc />
    public override Task Filtered_include_multiple_multi_level_includes_with_first_level_using_filter_include_on_one_of_the_chains_only(
        bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_multiple_multi_level_includes_with_first_level_using_filter_include_on_one_of_the_chains_only(async));

    /// <inheritdoc />
    public override Task Filtered_include_same_filter_set_on_same_navigation_twice_followed_by_ThenIncludes(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_same_filter_set_on_same_navigation_twice_followed_by_ThenIncludes(async));

    /// <inheritdoc />
    public override Task Filtered_include_complex_three_level_with_middle_having_filter2(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_complex_three_level_with_middle_having_filter2(async));

    /// <inheritdoc />
    public override Task Complex_query_with_let_collection_projection_FirstOrDefault(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Complex_query_with_let_collection_projection_FirstOrDefault(async));

    /// <inheritdoc />
    public override Task Take_Select_collection_Take(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Take_Select_collection_Take(async));

    /// <inheritdoc />
    public override Task Skip_Take_Select_collection_Skip_Take(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_Select_collection_Skip_Take(async));

    /// <inheritdoc />
    public override Task Filtered_include_Take_with_another_Take_on_top_level(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_Take_with_another_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_Skip_Take_with_another_Skip_Take_on_top_level(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_Skip_Take_with_another_Skip_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Skip_Take_Distinct_on_grouping_element(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_Distinct_on_grouping_element(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_inside_collection_projection(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_inside_collection_projection(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_with_collection_include(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_with_collection_include(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_with_reference_include(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_with_reference_include(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_FirstOrDefault_on_top_level(
        bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_FirstOrDefault_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_unordered_Take_on_top_level(
        bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_unordered_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task SelectMany_with_predicate_and_DefaultIfEmpty_projecting_root_collection_element_and_another_collection(
        bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .SelectMany_with_predicate_and_DefaultIfEmpty_projecting_root_collection_element_and_another_collection(async));

    /// <inheritdoc />
    public override Task Projecting_collection_with_group_by_after_optional_reference_correlated_with_parent(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Projecting_collection_with_group_by_after_optional_reference_correlated_with_parent(async));
}

/// <summary>
///     <c>ComplexNavigationsCollectionsSplitSharedTypeQueryRelationalTestBase</c> on Tier B — the
///     shared-type model, queried through its collections, with <c>AsSplitQuery</c> at every root.
/// </summary>
/// <remarks>
///     The non-shared-type sibling above carries the reading of why the hint changes nothing here
///     and why 4 overrides exist that EF's own split SQLite class does not need. This
///     class is that reading applied to the shared-type model, override for override.
/// </remarks>
public class ComplexNavigationsCollectionsSplitSharedTypeQueryInfoCarrierTest(
    ComplexNavigationsSharedTypeQueryInfoCarrierFixture fixture)
    : ComplexNavigationsCollectionsSplitSharedTypeQueryRelationalTestBase<
        ComplexNavigationsSharedTypeQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Complex_query_with_let_collection_projection_FirstOrDefault(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Complex_query_with_let_collection_projection_FirstOrDefault(async));

    /// <inheritdoc />
    public override Task Complex_query_with_let_collection_projection_FirstOrDefault_with_ToList_on_inner_and_outer(
        bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Complex_query_with_let_collection_projection_FirstOrDefault_with_ToList_on_inner_and_outer(async));

    /// <inheritdoc />
    public override Task Filtered_include_Skip_Take_with_another_Skip_Take_on_top_level(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_Skip_Take_with_another_Skip_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_Take_with_another_Take_on_top_level(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_Take_with_another_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_after_different_filtered_include_different_level(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_after_different_filtered_include_different_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_and_non_filtered_include_followed_by_then_include_on_same_navigation(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_and_non_filtered_include_followed_by_then_include_on_same_navigation(async));

    /// <inheritdoc />
    public override Task Filtered_include_complex_three_level_with_middle_having_filter1(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_complex_three_level_with_middle_having_filter1(async));

    /// <inheritdoc />
    public override Task Filtered_include_complex_three_level_with_middle_having_filter2(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_complex_three_level_with_middle_having_filter2(async));

    /// <inheritdoc />
    public override Task Filtered_include_multiple_multi_level_includes_with_first_level_using_filter_include_on_one_of_the_chains_only(
        bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_multiple_multi_level_includes_with_first_level_using_filter_include_on_one_of_the_chains_only(async));

    /// <inheritdoc />
    public override Task Filtered_include_same_filter_set_on_same_navigation_twice_followed_by_ThenIncludes(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_same_filter_set_on_same_navigation_twice_followed_by_ThenIncludes(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_FirstOrDefault_on_top_level(
        bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_FirstOrDefault_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_unordered_Take_on_top_level(
        bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_unordered_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Projecting_collection_with_group_by_after_optional_reference_correlated_with_parent(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Projecting_collection_with_group_by_after_optional_reference_correlated_with_parent(async));

    /// <inheritdoc />
    public override Task SelectMany_with_predicate_and_DefaultIfEmpty_projecting_root_collection_element_and_another_collection(
        bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .SelectMany_with_predicate_and_DefaultIfEmpty_projecting_root_collection_element_and_another_collection(async));

    /// <inheritdoc />
    public override Task Skip_Take_Distinct_on_grouping_element(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_Distinct_on_grouping_element(async));

    /// <inheritdoc />
    public override Task Skip_Take_Select_collection_Skip_Take(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_Select_collection_Skip_Take(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_inside_collection_projection(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_inside_collection_projection(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_with_collection_include(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_with_collection_include(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_with_reference_include(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_with_reference_include(async));

    /// <inheritdoc />
    public override Task Take_Select_collection_Take(bool async)
        => ComplexNavigationsSharedTypeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Take_Select_collection_Take(async));
}

/// <summary>
///     The shared-type complex-navigations fixture, wired to a SQLite backend behind the wire.
///     Shared by both classes above, exactly as EF shares its own.
/// </summary>
public class ComplexNavigationsSharedTypeQueryInfoCarrierFixture : ComplexNavigationsSharedTypeQueryRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}
