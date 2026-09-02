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
///     <c>NorthwindSplitIncludeQueryTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         The whole <c>NorthwindInclude</c> corpus again, with <c>AsSplitQuery</c> appended to
///         every query the base sends. This provider does not split, and
///         <c>QuerySplitter</c> removes the hint before the boundary analysis, so the server
///         receives the same single query <c>NorthwindIncludeQueryInfoCarrierTest</c> sends and
///         every answer is the same.
///     </para>
///     <para>
///         <b>The four overrides are EF's own</b> — <c>NorthwindSplitIncludeQuerySqliteTest</c>
///         carries exactly these four and no others, and they are the same four the unsplit class
///         carries. SQLite has no <c>APPLY</c>, split or not.
///     </para>
/// </remarks>
public class NorthwindSplitIncludeQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindSplitIncludeQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
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

    /// <summary>
    ///     The query reaches SQL and asks SQLite for <c>APPLY</c>, which it does not have. Shared
    ///     with the no-tracking class below, as EF shares the same four overrides between its own
    ///     two.
    /// </summary>
    internal static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);
}

/// <summary>
///     <c>NorthwindSplitIncludeNoTrackingQueryTestBase</c> on Tier B — the same corpus, untracked.
/// </summary>
/// <remarks>
///     The tracking sibling above carries the reading. This is the only place the no-tracking
///     include corpus runs against a real database: <c>NorthwindIncludeNoTrackingQueryTestBase</c>
///     itself is on Tier A, and a base belongs to exactly one tier.
/// </remarks>
public class NorthwindSplitIncludeNoTrackingQueryInfoCarrierTest(
    NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindSplitIncludeNoTrackingQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override Task Include_collection_with_cross_apply_with_filter(bool async)
        => NorthwindSplitIncludeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Include_collection_with_cross_apply_with_filter(async));

    /// <inheritdoc />
    public override Task Include_collection_with_outer_apply_with_filter(bool async)
        => NorthwindSplitIncludeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Include_collection_with_outer_apply_with_filter(async));

    /// <inheritdoc />
    public override Task Include_collection_with_outer_apply_with_filter_non_equality(bool async)
        => NorthwindSplitIncludeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Include_collection_with_outer_apply_with_filter_non_equality(async));

    /// <inheritdoc />
    public override Task Filtered_include_with_multiple_ordering(bool async)
        => NorthwindSplitIncludeQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Filtered_include_with_multiple_ordering(async));
}
