// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <see cref="NorthwindSelectQueryRelationalTestBase{TFixture}" /> on ADR-009 Tier B (SQLite).
/// </summary>
/// <remarks>
///     Derives from the <em>relational</em> base (#56), not the core one. That base swaps in
///     <c>RelationalQueryAsserter</c> and turns two core tests into translation-failure
///     assertions — <c>Reverse_without_explicit_ordering</c> (which this class used to restate by
///     hand, with a comment that it "cannot derive from" the relational base; it now can, because
///     the fixture implements <c>ITestSqlLoggerFactory</c> since R18) and
///     <c>Select_bool_closure_with_order_by_property_with_cast_to_nullable</c>. It adds no test the
///     core base lacks. Overrides only for what a run actually showed. Red here is information
///     (CLAUDE.md), not a regression.
/// </remarks>
public class NorthwindSelectQuerySqliteInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindSelectQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    public static Type ConstructorProjectionType => typeof(CustomerListItem);

    /// <inheritdoc />
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 169, 173,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_over_inline_array_projecting_range_variable_and_outer(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.SelectMany_over_inline_array_projecting_range_variable_and_outer(async))).Message);

    // -------------------------------------------------------------------------------------
    // BACKING-STORE LIMITATION — SQLite has no APPLY, and a correlated collection projection
    // needs one. EF Core's own Northwind*QuerySqliteTest overrides each of these the same way,
    // asserting the provider's own message; the limitation is SQLite's, not InfoCarrier's, and
    // a local SQLite provider fails identically with no wire involved.
    //
    // These do NOT carry over to Tier C (SQL Server, roadmap M7), which supports APPLY.
    // -------------------------------------------------------------------------------------

    // Answered here until 2026-09-22: the inner collection's read of the outer row was carried
    // outside it, so the server never needed APPLY. See ProjectionRewriter._enclosing.
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 246, 249,
        Justification = Upstream.GaveNoReason)]
    public override async Task Projecting_after_navigation_and_distinct(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Projecting_after_navigation_and_distinct(async))).Message);

    // Answered here until 2026-09-22: every projection after the Distinct ran on the client, so
    // the server never saw the correlated collection. See ProjectionRewriter.TryFuseSelectWithReassembly.
    // The third is EF's relational refusal rather than APPLY, and EF's SQLite class asserts it.
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 262, 266,
        Justification = Upstream.GaveNoReason)]
    public override async Task Correlated_collection_after_distinct_not_containing_original_identifier(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Correlated_collection_after_distinct_not_containing_original_identifier(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 275, 280,
        Justification = Upstream.GaveNoReason)]
    public override async Task Correlated_collection_after_distinct_with_complex_projection_containing_original_identifier(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Correlated_collection_after_distinct_with_complex_projection_containing_original_identifier(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 268, 273,
        Justification = Upstream.GaveNoReason)]
    public override async Task Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier(bool async)
        => Assert.Equal(
            RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 312, 316,
        Justification = Upstream.GaveNoReason)]
    public override async Task Collection_projection_selecting_outer_element_followed_by_take(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Collection_projection_selecting_outer_element_followed_by_take(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 288, 291,
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_projection_subquery(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_projection_subquery(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 293, 296,
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_projection_subquery_single_result(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_projection_subquery_single_result(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 298, 301,
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_SelectMany_with_Take(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_SelectMany_with_Take(async))).Message);

    // Reached SQL only once the correlated-subquery rewrite (X5) stopped the split from refusing
    // it outright. EF's SQLite suite has overridden it all along.
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 240, 244,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_whose_selector_references_outer_source(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_whose_selector_references_outer_source(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 306, 310,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_with_collection_being_correlated_subquery_which_references_inner_and_outer_entity(
        bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_with_collection_being_correlated_subquery_which_references_inner_and_outer_entity(async)))
            .Message);

    // Same story as the join suite's `SelectMany_with_selecting_outer_element`: EF's SQLite tests
    // have always overridden this, and this provider only now translates far enough to hit it.
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 282, 286,
        Justification = Upstream.GaveNoReason)]
    public override async Task Select_nested_collection_deep_distinct_no_identifiers(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Select_nested_collection_deep_distinct_no_identifiers(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 251, 254,
        Justification = Upstream.GaveNoReason)]
    public override async Task Select_nested_collection_deep(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Select_nested_collection_deep(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 164, 167,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_1(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_1(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 175, 178,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_2(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_2(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 180, 183,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_3(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_3(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 185, 188,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_4(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_4(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 190, 193,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_5(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_5(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 195, 198,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_6(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_6(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 200, 203,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_7(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_7(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 335, 338,
        Justification = Upstream.GaveNoReason)]
    public override async Task Set_operation_in_pending_collection(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Set_operation_in_pending_collection(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 324, 327,
        Justification = Upstream.GaveNoReason)]
    public override async Task Take_on_correlated_collection_in_first(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Take_on_correlated_collection_in_first(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 318, 322,
        Justification = Upstream.GaveNoReason)]
    public override async Task Take_on_top_level_and_on_collection_projection_with_outer_apply(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Take_on_top_level_and_on_collection_projection_with_outer_apply(async))).Message);

    // Answered here until 2026-09-27: the grouping on an anonymous key ran on the client, so the
    // server never saw the correlated collection. See TransparentIdentifierRewriter.Rewriter.RebuildEach.
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 256, 260,
        Justification = Upstream.GaveNoReason)]
    public override async Task Correlated_collection_after_groupby_with_complex_projection_containing_original_identifier(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Correlated_collection_after_groupby_with_complex_projection_containing_original_identifier(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 329, 333,
        Justification = Upstream.GaveNoReason)]
    public override async Task Correlated_collection_after_groupby_with_complex_projection_not_containing_original_identifier(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Correlated_collection_after_groupby_with_complex_projection_not_containing_original_identifier(async))).Message);

    // -------------------------------------------------------------------------------------
    // BACKING-STORE LIMITATION — not APPLY this time, and each is EF Core's own override for
    // the same backend, adopted verbatim now that the split ships the whole query.
    // -------------------------------------------------------------------------------------

    // `Reverse` with nothing to reverse (Reverse_without_explicit_ordering) is now inherited
    // from NorthwindSelectQueryRelationalTestBase, which asserts exactly this failure.

    // EF's NorthwindSelectQuerySqliteTest asserts exactly this failure for exactly this test.
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 303, 304,
        Justification = Upstream.GaveNoReason)]
    public override Task Member_binding_after_ctor_arguments_fails_with_client_eval(bool async)
        => AssertTranslationFailed(() => base.Member_binding_after_ctor_arguments_fails_with_client_eval(async));

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 153, 162,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task
        SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
            bool async)
        => AssertUnableToTranslateEFProperty(
            () => base
                .SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
                    async));
}
