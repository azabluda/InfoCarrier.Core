// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

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
    // -------------------------------------------------------------------------------------
    // BACKING-STORE LIMITATION — SQLite has no APPLY, and a correlated collection projection
    // needs one. EF Core's own Northwind*QuerySqliteTest overrides each of these the same way,
    // asserting the provider's own message; the limitation is SQLite's, not InfoCarrier's, and
    // a local SQLite provider fails identically with no wire involved.
    //
    // These do NOT carry over to Tier C (SQL Server, roadmap M7), which supports APPLY.
    // -------------------------------------------------------------------------------------

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 308, 312,
        Justification = Upstream.GaveNoReason)]
    public override async Task Collection_projection_selecting_outer_element_followed_by_take(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Collection_projection_selecting_outer_element_followed_by_take(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 284, 287,
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_projection_subquery(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_projection_subquery(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 289, 292,
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_projection_subquery_single_result(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_projection_subquery_single_result(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 294, 297,
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_SelectMany_with_Take(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_SelectMany_with_Take(async))).Message);

    // Reached SQL only once the correlated-subquery rewrite (X5) stopped the split from refusing
    // it outright. EF's SQLite suite has overridden it all along.
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 236, 240,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_whose_selector_references_outer_source(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_whose_selector_references_outer_source(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 302, 306,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 278, 282,
        Justification = Upstream.GaveNoReason)]
    public override async Task Select_nested_collection_deep_distinct_no_identifiers(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Select_nested_collection_deep_distinct_no_identifiers(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 247, 250,
        Justification = Upstream.GaveNoReason)]
    public override async Task Select_nested_collection_deep(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Select_nested_collection_deep(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 166, 169,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_1(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_1(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 171, 174,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_2(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_2(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 176, 179,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_3(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_3(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 181, 184,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_4(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_4(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 186, 189,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_5(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_5(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 191, 194,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_6(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_6(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 196, 199,
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_7(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_7(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 331, 334,
        Justification = Upstream.GaveNoReason)]
    public override async Task Set_operation_in_pending_collection(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Set_operation_in_pending_collection(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 320, 323,
        Justification = Upstream.GaveNoReason)]
    public override async Task Take_on_correlated_collection_in_first(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Take_on_correlated_collection_in_first(async))).Message);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 314, 318,
        Justification = Upstream.GaveNoReason)]
    public override async Task Take_on_top_level_and_on_collection_projection_with_outer_apply(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Take_on_top_level_and_on_collection_projection_with_outer_apply(async))).Message);

    // -------------------------------------------------------------------------------------
    // BACKING-STORE LIMITATION — not APPLY this time, and each is EF Core's own override for
    // the same backend, adopted verbatim now that the split ships the whole query.
    // -------------------------------------------------------------------------------------

    // `Reverse` with nothing to reverse (Reverse_without_explicit_ordering) is now inherited
    // from NorthwindSelectQueryRelationalTestBase, which asserts exactly this failure.

    // EF's NorthwindSelectQuerySqliteTest asserts exactly this failure for exactly this test.
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs", 155, 164,
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
