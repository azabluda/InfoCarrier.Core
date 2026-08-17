// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <see cref="NorthwindSelectQueryTestBase{TFixture}" /> on ADR-009 Tier B (SQLite).
/// </summary>
/// <remarks>
///     Overrides only for what a run actually showed. Whether the Tier A class's overrides still apply on a backend that
///     genuinely translates is a question this class answers by measurement; overrides are added
///     only for what a run actually shows, with the reason stated. Red here is information
///     (CLAUDE.md), not a regression.
/// </remarks>
public class NorthwindSelectQuerySqliteInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindSelectQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    // -------------------------------------------------------------------------------------
    // BACKING-STORE LIMITATION — SQLite has no APPLY, and a correlated collection projection
    // needs one. EF Core's own Northwind*QuerySqliteTest overrides each of these the same way,
    // asserting the provider's own message; the limitation is SQLite's, not InfoCarrier's, and
    // a local SQLite provider fails identically with no wire involved.
    //
    // These do NOT carry over to Tier C (SQL Server, roadmap M7), which supports APPLY.
    // -------------------------------------------------------------------------------------

    public override async Task Collection_projection_selecting_outer_element_followed_by_take(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Collection_projection_selecting_outer_element_followed_by_take(async))).Message);

    public override async Task Reverse_in_projection_subquery(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_projection_subquery(async))).Message);

    public override async Task Reverse_in_projection_subquery_single_result(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_projection_subquery_single_result(async))).Message);

    public override async Task Reverse_in_SelectMany_with_Take(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_SelectMany_with_Take(async))).Message);

    // Reached SQL only once the correlated-subquery rewrite (X5) stopped the split from refusing
    // it outright. EF's SQLite suite has overridden it all along.
    public override async Task SelectMany_whose_selector_references_outer_source(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_whose_selector_references_outer_source(async))).Message);

    public override async Task SelectMany_with_collection_being_correlated_subquery_which_references_inner_and_outer_entity(
        bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_with_collection_being_correlated_subquery_which_references_inner_and_outer_entity(async)))
            .Message);

    // Same story as the join suite's `SelectMany_with_selecting_outer_element`: EF's SQLite tests
    // have always overridden this, and this provider only now translates far enough to hit it.
    public override async Task Select_nested_collection_deep_distinct_no_identifiers(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Select_nested_collection_deep_distinct_no_identifiers(async))).Message);

    public override async Task Select_nested_collection_deep(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Select_nested_collection_deep(async))).Message);

    public override async Task SelectMany_correlated_with_outer_1(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_1(async))).Message);

    public override async Task SelectMany_correlated_with_outer_2(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_2(async))).Message);

    public override async Task SelectMany_correlated_with_outer_3(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_3(async))).Message);

    public override async Task SelectMany_correlated_with_outer_4(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_4(async))).Message);

    public override async Task SelectMany_correlated_with_outer_5(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_5(async))).Message);

    public override async Task SelectMany_correlated_with_outer_6(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_6(async))).Message);

    public override async Task SelectMany_correlated_with_outer_7(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_7(async))).Message);

    public override async Task Set_operation_in_pending_collection(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Set_operation_in_pending_collection(async))).Message);

    public override async Task Take_on_correlated_collection_in_first(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Take_on_correlated_collection_in_first(async))).Message);

    public override async Task Take_on_top_level_and_on_collection_projection_with_outer_apply(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Take_on_top_level_and_on_collection_projection_with_outer_apply(async))).Message);

    // -------------------------------------------------------------------------------------
    // BACKING-STORE LIMITATION — not APPLY this time, and each is EF Core's own override for
    // the same backend, adopted verbatim now that the split ships the whole query.
    // -------------------------------------------------------------------------------------

    // `Reverse` with nothing to reverse: SQL has no row order to invert. Every relational
    // provider fails this, so EF puts the override on NorthwindSelectQueryRelationalTestBase --
    // a base this class cannot derive from, because it also swaps in a RelationalQueryAsserter
    // that needs relational test infrastructure. The assertion is the one that base makes.
    public override Task Reverse_without_explicit_ordering(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Reverse_without_explicit_ordering(async),
            RelationalStrings.MissingOrderingInSelectExpression);

    // EF's NorthwindSelectQuerySqliteTest asserts exactly this failure for exactly this test.
    public override Task
        SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
            bool async)
        => AssertUnableToTranslateEFProperty(
            () => base
                .SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
                    async));
}
