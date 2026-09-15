// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
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
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L308-L312",
        Justification = Upstream.GaveNoReason)]
    public override async Task Collection_projection_selecting_outer_element_followed_by_take(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Collection_projection_selecting_outer_element_followed_by_take(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L284-L287",
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_projection_subquery(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_projection_subquery(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L289-L292",
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_projection_subquery_single_result(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_projection_subquery_single_result(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L294-L297",
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_SelectMany_with_Take(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Reverse_in_SelectMany_with_Take(async))).Message);

    // Reached SQL only once the correlated-subquery rewrite (X5) stopped the split from refusing
    // it outright. EF's SQLite suite has overridden it all along.
    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L236-L240",
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_whose_selector_references_outer_source(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_whose_selector_references_outer_source(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L302-L306",
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
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L278-L282",
        Justification = Upstream.GaveNoReason)]
    public override async Task Select_nested_collection_deep_distinct_no_identifiers(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Select_nested_collection_deep_distinct_no_identifiers(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L247-L250",
        Justification = Upstream.GaveNoReason)]
    public override async Task Select_nested_collection_deep(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Select_nested_collection_deep(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L166-L169",
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_1(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_1(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L171-L174",
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_2(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_2(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L176-L179",
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_3(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_3(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L181-L184",
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_4(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_4(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L186-L189",
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_5(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_5(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L191-L194",
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_6(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_6(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L196-L199",
        Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_correlated_with_outer_7(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_correlated_with_outer_7(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L331-L334",
        Justification = Upstream.GaveNoReason)]
    public override async Task Set_operation_in_pending_collection(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Set_operation_in_pending_collection(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L320-L323",
        Justification = Upstream.GaveNoReason)]
    public override async Task Take_on_correlated_collection_in_first(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Take_on_correlated_collection_in_first(async))).Message);

    [StoreLimit(
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L314-L318",
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
        Upstream.EfCore + "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSelectQuerySqliteTest.cs#L155-L164",
        Justification = Upstream.GaveNoReason,
        Deviation = Deviations.SqlNotAsserted)]
    public override Task
        SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
            bool async)
        => AssertUnableToTranslateEFProperty(
            () => base
                .SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
                    async));

    // -------------------------------------------------------------------------------------
    // THIS PROVIDER ANSWERS A QUERY EF's RELATIONAL PROVIDERS REJECT.
    // NorthwindSelectQueryRelationalTestBase turns this into AssertTranslationFailed: a
    // relational provider cannot ORDER BY a client-side constant projection. This provider's
    // split evaluates the ordering over the constant on the client (it is a no-op over a single
    // distinct value) and the server runs the rest, so the query returns the correct rows.
    // Restored to the core assertion — the answer is checked, and it is right. Same category as
    // limitations.md's "queries this provider answers that other EF providers refuse".
    // -------------------------------------------------------------------------------------
    [InfoCarrierDesign(
        "ADR-010",
        Justification = "The ordering follows a projection of a closure value, so it runs on the client over the rows "
            + "the server returned, and the query is answered where EF's relational providers refuse to translate it.",
        UpstreamTest = Upstream.EfCore + "test/EFCore.Relational.Specification.Tests/Query/NorthwindSelectQueryRelationalTestBase.cs#L11-L12",
        Deviation = "EF's relational base asserts the translation failure. This override is the core base's body, "
            + "which asserts the ordered rows.")]
    public override async Task Select_bool_closure_with_order_by_property_with_cast_to_nullable(bool async)
    {
        var boolean = false;

        await AssertQuery(
            async,
            ss => ss.Set<Customer>().Select(c => new { f = boolean }).OrderBy(e => (bool?)e.f),
            assertOrder: true);
    }
}
