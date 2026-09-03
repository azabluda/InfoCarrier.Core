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
///     <see cref="NorthwindJoinQueryRelationalTestBase{TFixture}" /> on ADR-009 Tier B (SQLite).
/// </summary>
/// <remarks>
///     Derives from the <em>relational</em> base (#56), not the core one. The relational base only
///     swaps in <c>RelationalQueryAsserter</c> — it adds no test the core base lacks — so the
///     bare move changes no count; it exists to pick up the relational base's expected-answer
///     corrections as EF adds them, the same reason R18 and R20 took the other Northwind bases.
///     Overrides only for what a run actually showed. Red here is information (CLAUDE.md), not a
///     regression.
/// </remarks>
public class NorthwindJoinQuerySqliteInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindJoinQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    // -------------------------------------------------------------------------------------
    // BACKING-STORE LIMITATION — SQLite has no APPLY, and a correlated collection projection
    // needs one. EF Core's own Northwind*QuerySqliteTest overrides each of these the same way,
    // asserting the provider's own message; the limitation is SQLite's, not InfoCarrier's, and
    // a local SQLite provider fails identically with no wire involved.
    //
    // These do NOT carry over to Tier C (SQL Server, roadmap M7), which supports APPLY.
    // -------------------------------------------------------------------------------------

    public override async Task SelectMany_with_selecting_outer_entity(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_with_selecting_outer_entity(async))).Message);

    // Reached SQL only once ADR-011's carrier re-carry stopped the split from client-evaluating
    // it. EF's own SQLite suite has had this override all along
    // (`NorthwindJoinQuerySqliteTest.SelectMany_with_selecting_outer_element`); this provider
    // simply was not getting far enough to need it.
    public override async Task SelectMany_with_selecting_outer_element(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_with_selecting_outer_element(async))).Message);

    public override async Task SelectMany_with_selecting_outer_entity_column_and_inner_column(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_with_selecting_outer_entity_column_and_inner_column(async))).Message);

    public override async Task Take_in_collection_projection_with_FirstOrDefault_on_top_level(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Take_in_collection_projection_with_FirstOrDefault_on_top_level(async))).Message);

    // The same three EF's SQLite suite has: hoisting the collection projection lets the query
    // reach SQL, and SQLite declines the APPLY it needs.
    public override async Task SelectMany_with_client_eval(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_with_client_eval(async))).Message);

    public override async Task SelectMany_with_client_eval_with_collection_shaper(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_with_client_eval_with_collection_shaper(async))).Message);

    public override async Task SelectMany_with_client_eval_with_collection_shaper_ignored(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_with_client_eval_with_collection_shaper_ignored(async))).Message);
}
