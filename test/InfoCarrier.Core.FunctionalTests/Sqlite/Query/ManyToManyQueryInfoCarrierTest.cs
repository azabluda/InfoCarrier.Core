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
///     Many-to-many navigations over TPT and TPC hierarchies, tracking and no-tracking, on ADR-009
///     <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Four bases, two fixtures.</b> The no-tracking bases take the same fixture as the
///         tracking ones, which is how EF wires its own SQLite classes: the store and model are
///         identical and only the tracking behaviour differs. <b>All four carry the overrides
///         below</b>, as EF's four do; a filtered probe that matched only the tracking pair
///         reported half the failures, and the full run is what caught it.
///     </para>
///     <para>
///         <b>The two <c>ApplyNotSupported</c> overrides are EF's own, adopted after measuring
///         rather than copied in advance.</b> <c>APPLY</c> is not SQLite syntax, EF's own SQLite
///         classes override these same two theories, and CLAUDE.md calls a newly-red test that EF
///         also overrides convergence with the reference provider rather than a defect here. They
///         were left out of the first run on purpose: writing an override before measuring imports
///         a workaround whose limitation might not reach across a wire, and this repository has
///         already deleted one that outlived its cause.
///     </para>
///     <para>
///         <b>The <c>_split</c> variants are NOT overridden, and that is the finding.</b> EF does
///         not override them, because on a relational client <c>AsSplitQuery</c> avoids the
///         <c>APPLY</c> altogether. Here the marker never reaches the server (#60), so the split
///         variants run as one query and fail the same way. Together with
///         <c>Include_on_derived_type_with_queryable_Cast_split</c> next door, which returns an
///         over-included graph instead of throwing, these are the first measured price of the
///         missing marker: eight tests, in two different shapes.
///     </para>
/// </remarks>
public class TPTManyToManyQueryInfoCarrierTest(TPTManyToManyQueryInfoCarrierFixture fixture)
    : TPTManyToManyQueryRelationalTestBase<TPTManyToManyQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where(bool async)
        => AssertApplyNotSupported(() => base
            .Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where(async));

    /// <inheritdoc />
    public override Task Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_EF_Property(
        bool async)
        => AssertApplyNotSupported(() => base
            .Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_EF_Property(async));

    internal static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);
}

/// <inheritdoc cref="TPTManyToManyQueryInfoCarrierTest" />
public class TPTManyToManyNoTrackingQueryInfoCarrierTest(TPTManyToManyQueryInfoCarrierFixture fixture)
    : TPTManyToManyNoTrackingQueryRelationalTestBase<TPTManyToManyQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where(bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base
            .Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where(async));

    /// <inheritdoc />
    public override Task Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_EF_Property(
        bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base
            .Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_EF_Property(async));
}

/// <inheritdoc cref="TPTManyToManyQueryInfoCarrierTest" />
public class TPCManyToManyQueryInfoCarrierTest(TPCManyToManyQueryInfoCarrierFixture fixture)
    : TPCManyToManyQueryRelationalTestBase<TPCManyToManyQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where(bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base
            .Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where(async));

    /// <inheritdoc />
    public override Task Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_EF_Property(
        bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base
            .Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_EF_Property(async));
}

/// <inheritdoc cref="TPTManyToManyQueryInfoCarrierTest" />
public class TPCManyToManyNoTrackingQueryInfoCarrierTest(TPCManyToManyQueryInfoCarrierFixture fixture)
    : TPCManyToManyNoTrackingQueryRelationalTestBase<TPCManyToManyQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where(bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base
            .Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where(async));

    /// <inheritdoc />
    public override Task Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_EF_Property(
        bool async)
        => TPTManyToManyQueryInfoCarrierTest.AssertApplyNotSupported(() => base
            .Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_EF_Property(async));
}

/// <summary>
///     The TPT many-to-many fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class TPTManyToManyQueryInfoCarrierFixture : TPTManyToManyQueryRelationalFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}

/// <summary>
///     The TPC many-to-many fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class TPCManyToManyQueryInfoCarrierFixture : TPCManyToManyQueryRelationalFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}
