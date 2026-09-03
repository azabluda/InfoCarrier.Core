// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
///     <c>NorthwindMiscellaneousQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         The largest single query base in the suite, and the last big one still on Tier A. The
///         relational base adds exactly two tests of its own, both <c>AsSplitQuery</c>, which R41
///         priced as the whole reason to stand aside from it; R59 makes them free and both pass.
///     </para>
///     <para>
///         <b>Ten overrides were deleted by the move, which is the tier rule paying out.</b> Seven
///         asserted <em>"Sequence contains no elements"</em> — true of the InMemory store that used
///         to sit behind this wire, which throws where a relational store returns an empty
///         sequence, so the base's own expectation could not hold and had to be neutered. The
///         other three were EF's own InMemory suppressions of
///         <c>Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_*</c>,
///         which InMemory cannot compose and SQLite can.
///     </para>
///     <para>
///         <b>Nine overrides arrive, and every one is EF's own
///         <c>NorthwindMiscellaneousQuerySqliteTest</c>.</b> Five are SQLite's missing
///         <c>APPLY</c>, two are its date arithmetic, and one is a date component it cannot
///         translate at all. EF overrides 36 tests here and this class overrides nine, because the
///         other 27 pass — an override is written where a test fails, not where the reference
///         provider has one.
///     </para>
/// </remarks>
public class NorthwindMiscellaneousQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindMiscellaneousQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    // Category 4 — client evaluation outside the final projection. The base expects success
    // because it can be client-evaluated in-process; a remoting provider would have to fetch the
    // whole table to do the same. EF Core's own SQLite class overrides this identically, details
    // clause included, and only the fixture named in the message differs.

    /// <inheritdoc />
    public override Task Client_code_unknown_method(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Client_code_unknown_method(async),
            CoreStrings.QueryUnableToTranslateMethod(
                "Microsoft.EntityFrameworkCore.Query.NorthwindMiscellaneousQueryTestBase<"
                    + "InfoCarrier.Core.FunctionalTests.TestUtilities.NorthwindQueryInfoCarrierSqliteFixture<"
                    + "Microsoft.EntityFrameworkCore.TestUtilities.NoopModelCustomizer>>",
                nameof(UnknownMethod)));

    /// <inheritdoc />
    public override Task Max_on_empty_sequence_throws(bool async)
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Max_on_empty_sequence_throws(async));

    /// <inheritdoc />
    public override async Task Entity_equality_through_subquery_composite_key(bool async)
        => Assert.Equal(
            CoreStrings.EntityEqualityOnCompositeKeyEntitySubqueryNotSupported("==", nameof(OrderDetail)),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Entity_equality_through_subquery_composite_key(async)))
            .Message);

    // -------------------------------------------------------------------------------------
    // STORE LIMITATION -- SQLite has no APPLY. EF's own NorthwindMiscellaneousQuerySqliteTest
    // overrides all five, four of them with this same assertion. The fifth,
    // SelectMany_correlated_subquery_hard, EF disables outright by returning a null Task; the
    // query fails here for the reason its four siblings do, and saying so is more informative
    // than copying a skip.
    // -------------------------------------------------------------------------------------

    /// <inheritdoc />
    public override Task DefaultIfEmpty_in_subquery_nested_filter_order_comparison(bool async)
        => AssertApplyNotSupported(() => base.DefaultIfEmpty_in_subquery_nested_filter_order_comparison(async));

    /// <inheritdoc />
    public override Task SelectMany_correlated_subquery_hard(bool async)
        => AssertApplyNotSupported(() => base.SelectMany_correlated_subquery_hard(async));

    /// <inheritdoc />
    public override Task SelectMany_correlated_with_Select_value_type_and_DefaultIfEmpty_in_selector(bool async)
        => AssertApplyNotSupported(
            () => base.SelectMany_correlated_with_Select_value_type_and_DefaultIfEmpty_in_selector(async));

    /// <inheritdoc />
    public override Task Select_correlated_subquery_ordered(bool async)
        => AssertApplyNotSupported(() => base.Select_correlated_subquery_ordered(async));

    /// <inheritdoc />
    public override Task Select_subquery_recursive_trivial(bool async)
        => AssertApplyNotSupported(() => base.Select_subquery_recursive_trivial(async));

    // -------------------------------------------------------------------------------------
    // STORE LIMITATION -- SQLite's date handling. All three are EF's own, with its golden-SQL
    // assertions dropped: what crosses this wire is the answer, not the dialect.
    // -------------------------------------------------------------------------------------

    /// <inheritdoc />
    /// <remarks>
    ///     SQLite adds months differently from everyone else — adding one month to 31 January
    ///     gives 2 or 3 March rather than the 28th or 29th of February (EF issue #25851) — so the
    ///     comparison is a bounded difference rather than an equality.
    /// </remarks>
    public override Task Select_expression_datetime_add_month(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>()
                .Where(o => o.OrderDate != null)
                .Select(o => new Order { OrderDate = o.OrderDate!.Value.AddMonths(1) }),
            e => e.OrderDate!,
            elementAsserter: (e, a) =>
            {
                Assert.Equal(e.OrderDate.HasValue, a.OrderDate.HasValue);
                if (e.OrderDate.HasValue && a.OrderDate.HasValue)
                {
                    TimeSpan diff = (e.OrderDate - a.OrderDate)!.Value;
                    Assert.True(diff.Days is >= -3 and <= 0);
                    Assert.Equal(0, diff.Hours);
                    Assert.Equal(0, diff.Minutes);
                    Assert.Equal(0, diff.Seconds);
                    Assert.Equal(0, diff.Milliseconds);
                    Assert.Equal(0, diff.Microseconds);
                }
            });

    /// <inheritdoc />
    /// <remarks>
    ///     SQLite is inaccurate below one second, so EF's own class moves the scenario to whole
    ///     seconds rather than ticks.
    /// </remarks>
    public override Task Select_expression_datetime_add_ticks(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Order>().Where(o => o.OrderDate != null)
                .Select(o => new Order { OrderDate = o.OrderDate!.Value.AddTicks(10 * TimeSpan.TicksPerSecond) }),
            e => e.OrderDate!);

    /// <inheritdoc />
    public override Task Where_nanosecond_and_microsecond_component(bool async)
        => AssertTranslationFailed(() => base.Where_nanosecond_and_microsecond_component(async));

    private static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);
}
