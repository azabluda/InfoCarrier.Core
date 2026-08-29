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
///     <c>NorthwindIncludeQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A, and the move deletes the one override the old class had.</b> That
///         override asserted a translation failure for <c>RightJoin</c>, which InMemory cannot
///         translate and a relational store can. Its own remark said to delete rather than carry
///         it, and this is where that happens.
///     </para>
/// </remarks>
public class NorthwindIncludeQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindIncludeQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    // -------------------------------------------------------------------------------------
    // STORE LIMITATION -- SQLite has no APPLY. EF's own `NorthwindIncludeQuerySqliteTest`
    // overrides these four and no others, with the same assertion; the four measured red here
    // are the same four. A query reaching the store and being refused by it is convergence with
    // the reference provider, not a gap in this one (CLAUDE.md).
    // -------------------------------------------------------------------------------------

    /// <inheritdoc />
    public override Task Include_collection_with_cross_apply_with_filter(bool async)
        => AssertApplyNotSupported(() => base.Include_collection_with_cross_apply_with_filter(async));

    /// <inheritdoc />
    public override Task Include_collection_with_outer_apply_with_filter(bool async)
        => AssertApplyNotSupported(() => base.Include_collection_with_outer_apply_with_filter(async));

    /// <inheritdoc />
    public override Task Include_collection_with_outer_apply_with_filter_non_equality(bool async)
        => AssertApplyNotSupported(() => base.Include_collection_with_outer_apply_with_filter_non_equality(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_multiple_ordering(bool async)
        => AssertApplyNotSupported(() => base.Filtered_include_with_multiple_ordering(async));

    private static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);
}
