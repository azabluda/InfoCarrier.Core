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
///     <c>ComplexNavigationsQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         The deepest navigation corpus EF has: optional and required one-to-one chains four
///         levels down, each level a separate entity type. Where <c>InheritanceRelationships</c>
///         tests which type a navigation names, this tests how far a query can walk before the
///         split has to decide what travels.
///     </para>
///     <para>
///         <b>Moved from Tier A under the Group C policy.</b> The relational base adds no test
///         methods of its own, so the move is not about new tests. It is about the ones that
///         already existed running against a real database rather than InMemory. Neither the core
///         base nor the relational one declares <c>UseTransaction</c> or calls
///         <c>ExecuteWithStrategyInTransactionAsync</c>, both checked rather than assumed, so
///         nothing here needs a transaction override.
///     </para>
///     <para>
///         <b>Every override below is one EF's own SQLite suite carries, bar one.</b> The bare
///         move measured 118 red across the four classes; 110 are SQLite's missing <c>APPLY</c>,
///         and the remaining eight are the two families at the foot of this class and its
///         shared-type sibling. Not one red was this provider's.
///     </para>
/// </remarks>
public class ComplexNavigationsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
    : ComplexNavigationsQueryRelationalTestBase<ComplexNavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Let_let_contains_from_outer_let(bool async)
        => AssertApplyNotSupported(() => base.Let_let_contains_from_outer_let(async));

    /// <inheritdoc />
    public override Task Prune_does_not_throw_null_ref(bool async)
        => AssertApplyNotSupported(() => base.Prune_does_not_throw_null_ref(async));

    /// <inheritdoc />
    public override Task Nested_SelectMany_correlated_with_join_table_correctly_translated_to_apply(bool async)
        => AssertApplyNotSupported(() => base.Nested_SelectMany_correlated_with_join_table_correctly_translated_to_apply(async));

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
    /// </remarks>
    public override Task GroupJoin_client_method_in_OrderBy(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.GroupJoin_client_method_in_OrderBy(async),
            CoreStrings.QueryUnableToTranslateMethod(
                "Microsoft.EntityFrameworkCore.Query.ComplexNavigationsQueryTestBase<"
                    + typeof(ComplexNavigationsQueryInfoCarrierFixture).FullName + ">",
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
///     <c>ComplexNavigationsCollectionsQueryRelationalTestBase</c> on Tier B — the same model,
///     queried through its collections.
/// </summary>
/// <remarks>
///     <para>
///         EF's relational base is a one-liner, and every override below is
///         <c>ComplexNavigationsCollectionsQuerySqliteTest</c>'s. The Tier A class this replaces
///         carried seven overrides for InMemory's non-composed <c>GroupBy</c> limitation; a real
///         database has no such limitation and all seven are gone.
///     </para>
///     <para>
///         <b>One of EF's SQLite overrides is deliberately absent.</b>
///         <c>Projecting_collection_after_optional_reference_correlated_with_parent</c> passes
///         here: the projection split reassembles that collection on the client, so the query
///         never asks SQLite for <c>APPLY</c>. This provider answers it where EF's SQLite provider
///         refuses, so the base's own answer-check stands.
///     </para>
/// </remarks>
public class ComplexNavigationsCollectionsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
    : ComplexNavigationsCollectionsQueryRelationalTestBase<ComplexNavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Complex_query_with_let_collection_projection_FirstOrDefault_with_ToList_on_inner_and_outer(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Complex_query_with_let_collection_projection_FirstOrDefault_with_ToList_on_inner_and_outer(async));

    /// <inheritdoc />
    public override Task Include_inside_subquery(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Include_inside_subquery(async));

    /// <inheritdoc />
    public override Task Filtered_include_after_different_filtered_include_different_level(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_after_different_filtered_include_different_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_outer_parameter_used_inside_filter(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_outer_parameter_used_inside_filter(async));

    /// <inheritdoc />
    public override Task Filtered_include_and_non_filtered_include_followed_by_then_include_on_same_navigation(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_and_non_filtered_include_followed_by_then_include_on_same_navigation(async));

    /// <inheritdoc />
    public override Task Filtered_include_multiple_multi_level_includes_with_first_level_using_filter_include_on_one_of_the_chains_only(
        bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_multiple_multi_level_includes_with_first_level_using_filter_include_on_one_of_the_chains_only(async));

    /// <inheritdoc />
    public override Task Filtered_include_same_filter_set_on_same_navigation_twice_followed_by_ThenIncludes(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_same_filter_set_on_same_navigation_twice_followed_by_ThenIncludes(async));

    /// <inheritdoc />
    public override Task Filtered_include_complex_three_level_with_middle_having_filter1(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_complex_three_level_with_middle_having_filter1(async));

    /// <inheritdoc />
    public override Task Filtered_include_complex_three_level_with_middle_having_filter2(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_complex_three_level_with_middle_having_filter2(async));

    /// <inheritdoc />
    public override Task Complex_query_with_let_collection_projection_FirstOrDefault(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Complex_query_with_let_collection_projection_FirstOrDefault(async));

    /// <inheritdoc />
    public override Task Take_Select_collection_Take(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Take_Select_collection_Take(async));

    /// <inheritdoc />
    public override Task Skip_Take_Select_collection_Skip_Take(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_Select_collection_Skip_Take(async));

    /// <inheritdoc />
    public override Task Filtered_include_Take_with_another_Take_on_top_level(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_Take_with_another_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_Skip_Take_with_another_Skip_Take_on_top_level(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_Skip_Take_with_another_Skip_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Skip_Take_Distinct_on_grouping_element(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_Distinct_on_grouping_element(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_inside_collection_projection(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_inside_collection_projection(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_with_collection_include(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_with_collection_include(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_with_reference_include(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_with_reference_include(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_FirstOrDefault_on_top_level(
        bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_FirstOrDefault_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_unordered_Take_on_top_level(
        bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_unordered_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task SelectMany_with_predicate_and_DefaultIfEmpty_projecting_root_collection_element_and_another_collection(
        bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .SelectMany_with_predicate_and_DefaultIfEmpty_projecting_root_collection_element_and_another_collection(async));

    /// <inheritdoc />
    public override Task Complex_query_issue_21665(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Complex_query_issue_21665(async));

    /// <inheritdoc />
    public override Task Projecting_collection_with_group_by_after_optional_reference_correlated_with_parent(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Projecting_collection_with_group_by_after_optional_reference_correlated_with_parent(async));
}

/// <summary>
///     <c>ComplexNavigationsCollectionsSplitQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b>
///     (#56) — the collections corpus again, with <c>AsSplitQuery</c> applied at every query root.
/// </summary>
/// <remarks>
///     <para>
///         The base injects the hint through <c>RewriteServerQueryExpression</c>, so all
///         23 of its tests run it. <b>This provider splits since R149</b>:
///         <c>QuerySplitter</c> still removes the hint before the boundary analysis -- it cannot
///         travel inside the tree -- but it now travels BESIDE it on
///         <c>QueryDataRequest.SplitQueryBehavior</c>, and <c>ServerQueryExecutor</c> re-applies it
///         to the rebuilt query where a real relational provider can honour it.
///     </para>
///     <para>
///         <b>Four overrides were deleted when that landed, and the paragraph they replace had
///         predicted it.</b> It read: "4 of them are the measured cost of not splitting -- a real
///         split query fetches those collections in a second statement and never asks for
///         <c>APPLY</c>". The second statement is real now, so
///         <c>Filtered_include_after_different_filtered_include_different_level</c>,
///         <c>Filtered_include_complex_three_level_with_middle_having_filter1</c>,
///         <c>Filtered_include_complex_three_level_with_middle_having_filter2</c> and
///         <c>Skip_Take_on_grouping_element_with_collection_include</c> pass, and EF's own SQLite
///         split class does not override them either. <b>An override of ours that EF does not have
///         is a workaround to delete once the limitation goes</b>, and this is that.
///     <para>
///         <b>One of EF's overrides is deliberately absent</b>, as in the unsplit class:
///         <c>Projecting_collection_after_optional_reference_correlated_with_parent</c> passes
///         here, because the projection split reassembles that collection on the client.
///     </para>
/// </remarks>
public class ComplexNavigationsCollectionsSplitQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
    : ComplexNavigationsCollectionsSplitQueryRelationalTestBase<ComplexNavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Complex_query_issue_21665(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Complex_query_issue_21665(async));

    /// <inheritdoc />
    public override Task Complex_query_with_let_collection_projection_FirstOrDefault(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Complex_query_with_let_collection_projection_FirstOrDefault(async));

    /// <inheritdoc />
    public override Task Complex_query_with_let_collection_projection_FirstOrDefault_with_ToList_on_inner_and_outer(
        bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Complex_query_with_let_collection_projection_FirstOrDefault_with_ToList_on_inner_and_outer(async));

    /// <inheritdoc />
    public override Task Filtered_include_Skip_Take_with_another_Skip_Take_on_top_level(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_Skip_Take_with_another_Skip_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_Take_with_another_Take_on_top_level(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_Take_with_another_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_and_non_filtered_include_followed_by_then_include_on_same_navigation(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_and_non_filtered_include_followed_by_then_include_on_same_navigation(async));

    /// <inheritdoc />
    public override Task Filtered_include_multiple_multi_level_includes_with_first_level_using_filter_include_on_one_of_the_chains_only(
        bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_multiple_multi_level_includes_with_first_level_using_filter_include_on_one_of_the_chains_only(async));

    /// <inheritdoc />
    public override Task Filtered_include_outer_parameter_used_inside_filter(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_outer_parameter_used_inside_filter(async));

    /// <inheritdoc />
    public override Task Filtered_include_same_filter_set_on_same_navigation_twice_followed_by_ThenIncludes(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_same_filter_set_on_same_navigation_twice_followed_by_ThenIncludes(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_FirstOrDefault_on_top_level(
        bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_FirstOrDefault_on_top_level(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_unordered_Take_on_top_level(
        bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .Filtered_include_with_Take_without_order_by_followed_by_ThenInclude_and_unordered_Take_on_top_level(async));

    /// <inheritdoc />
    public override Task Include_inside_subquery(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Include_inside_subquery(async));

    /// <inheritdoc />
    public override Task Projecting_collection_with_group_by_after_optional_reference_correlated_with_parent(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Projecting_collection_with_group_by_after_optional_reference_correlated_with_parent(async));

    /// <inheritdoc />
    public override Task SelectMany_with_predicate_and_DefaultIfEmpty_projecting_root_collection_element_and_another_collection(
        bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base
                .SelectMany_with_predicate_and_DefaultIfEmpty_projecting_root_collection_element_and_another_collection(async));

    /// <inheritdoc />
    public override Task Skip_Take_Distinct_on_grouping_element(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_Distinct_on_grouping_element(async));

    /// <inheritdoc />
    public override Task Skip_Take_Select_collection_Skip_Take(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_Select_collection_Skip_Take(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_inside_collection_projection(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_inside_collection_projection(async));

    /// <inheritdoc />
    public override Task Skip_Take_on_grouping_element_with_reference_include(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Skip_Take_on_grouping_element_with_reference_include(async));

    /// <inheritdoc />
    public override Task Take_Select_collection_Take(bool async)
        => ComplexNavigationsQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Take_Select_collection_Take(async));
}

/// <summary>
///     The complex-navigations fixture, wired to a SQLite backend behind the wire. Shared by both
///     classes above, exactly as EF shares its own.
/// </summary>
/// <remarks>
///     <c>ComplexNavigationsQueryRelationalFixtureBase</c> implements
///     <c>ITestSqlLoggerFactory</c>, which is what the compliance test's second assertion asks of
///     a relational query fixture.
/// </remarks>
public class ComplexNavigationsQueryInfoCarrierFixture : ComplexNavigationsQueryRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}
