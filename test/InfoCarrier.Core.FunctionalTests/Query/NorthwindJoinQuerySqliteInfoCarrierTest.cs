// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <see cref="NorthwindJoinQueryTestBase{TFixture}" /> on ADR-009 Tier B (SQLite).
/// </summary>
/// <remarks>
///     Overrides only for what a run actually showed. Whether the Tier A class's overrides still apply on a backend that
///     genuinely translates is a question this class answers by measurement; overrides are added
///     only for what a run actually shows, with the reason stated. Red here is information
///     (CLAUDE.md), not a regression.
/// </remarks>
public class NorthwindJoinQuerySqliteInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindJoinQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    // -------------------------------------------------------------------------------------
    // BACKING-STORE LIMITATION — SQLite has no APPLY, and a correlated collection projection
    // needs one. EF Core's own Northwind*QuerySqliteTest overrides each of these the same way,
    // asserting the provider's own message; the limitation is SQLite's, not InfoCarrier's, and
    // a local SQLite provider fails identically with no wire involved.
    //
    // These do NOT carry over to Tier C (SQL Server, roadmap M7), which supports APPLY.
    // -------------------------------------------------------------------------------------

    public override async Task Left_join_with_tautology_predicate_doesnt_convert_to_cross_join(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Left_join_with_tautology_predicate_doesnt_convert_to_cross_join(async))).Message);

    public override async Task SelectMany_with_selecting_outer_entity(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.SelectMany_with_selecting_outer_entity(async))).Message);

    public override async Task Take_in_collection_projection_with_FirstOrDefault_on_top_level(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Take_in_collection_projection_with_FirstOrDefault_on_top_level(async))).Message);
}
