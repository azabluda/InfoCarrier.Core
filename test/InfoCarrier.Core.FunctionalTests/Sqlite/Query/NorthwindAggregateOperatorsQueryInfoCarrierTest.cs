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
///     <c>NorthwindAggregateOperatorsQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A, and the move deletes eight overrides.</b> Each Tier A override
///         asserted an InMemory-store limitation — an aggregate over an empty subquery throwing,
///         a local <c>IEnumerable</c> in <c>Contains</c> with no translation — and the Tier A
///         class already said in its own remarks that these "do not apply to the relational
///         (SQLite) backend of ADR-009 Tier B and must be deleted, not carried over". The
///         relational base supplies its own message-shape overrides for the empty-subquery
///         aggregates and for <c>Last</c> without an <c>ORDER BY</c>.
///     </para>
///     <para>
///         The two overrides below are EF Core's own, adopted after measuring:
///         <c>Multiple_collection_navigation_with_FirstOrDefault_chained</c> needs <c>APPLY</c>,
///         which is not SQLite syntax, and <c>Contains</c> over a local array of tuples has no
///         translation. Both are convergence with EF's own <c>NorthwindAggregateOperatorsQuerySqliteTest</c>.
///     </para>
///     <para>
///         <b>Three members are left failing and tracked, not overridden.</b>
///         <c>Average_over_max_subquery</c>, <c>Average_over_nested_subquery</c> and
///         <c>Type_casting_inside_sum</c> return an aggregate that differs from EF's expected value
///         in the trailing digits: the <c>(decimal)</c> cast over an <c>int</c>/<c>float</c>
///         aggregate resolves to a different translation on the two sides of the wire (the B4
///         family in CLAUDE.md — a type mapping computed twice). Per ADR-004 a red spec test is
///         information; the divergence is recorded in <c>test/known-failures.txt</c> under R20.
///     </para>
/// </remarks>
public class NorthwindAggregateOperatorsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindAggregateOperatorsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override async Task Multiple_collection_navigation_with_FirstOrDefault_chained(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Multiple_collection_navigation_with_FirstOrDefault_chained(async))).Message);

    /// <inheritdoc />
    public override Task Contains_with_local_tuple_array_closure(bool async)
        => AssertTranslationFailed(() => base.Contains_with_local_tuple_array_closure(async));
}
