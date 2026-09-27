// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <see cref="NorthwindEFPropertyIncludeQueryTestBase{TFixture}" /> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run can
///     compare it with plain EF; Tier A's store runs no statement. Its one override there was EF's
///     InMemory <c>RightJoin</c> refusal, and its remark said it "must be deleted, not carried over"
///     when the base reached a relational store. It was. The overrides below are EF's own SQLite
///     ones, each adopted after it was measured red.
/// </remarks>
public class NorthwindEFPropertyIncludeQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindEFPropertyIncludeQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindEFPropertyIncludeQuerySqliteTest.cs", 14, 17,
        Justification = Upstream.GaveNoReason)]
    public override Task Filtered_include_with_multiple_ordering(bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Filtered_include_with_multiple_ordering(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindEFPropertyIncludeQuerySqliteTest.cs", 19, 23,
        Justification = Upstream.GaveNoReason)]
    public override Task Include_collection_with_cross_apply_with_filter(bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Include_collection_with_cross_apply_with_filter(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindEFPropertyIncludeQuerySqliteTest.cs", 25, 29,
        Justification = Upstream.GaveNoReason)]
    public override Task Include_collection_with_outer_apply_with_filter(bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Include_collection_with_outer_apply_with_filter(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindEFPropertyIncludeQuerySqliteTest.cs", 31, 35,
        Justification = Upstream.GaveNoReason)]
    public override Task Include_collection_with_outer_apply_with_filter_non_equality(bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base.Include_collection_with_outer_apply_with_filter_non_equality(async));

    /// <inheritdoc />
    /// <remarks>EF's own: a relational provider refuses <c>Last</c> without an ordering, where InMemory answers it.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindEFPropertyIncludeQuerySqliteTest.cs", 37, 40,
        Justification = Upstream.GaveNoReason)]
    public override async Task Include_collection_with_last_no_orderby(bool async)
        => Assert.Equal(
            RelationalStrings.LastUsedWithoutOrderBy(nameof(Enumerable.Last)),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Include_collection_with_last_no_orderby(async))).Message);
}
