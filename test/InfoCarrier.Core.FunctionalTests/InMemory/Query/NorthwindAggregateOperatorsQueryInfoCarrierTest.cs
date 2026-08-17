// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <see cref="NorthwindAggregateOperatorsQueryTestBase{TFixture}" /> over the InfoCarrier client with an InMemory backend.
/// </summary>
/// <remarks>
///     <para>
///         Each override mirrors EF Core's own <c>NorthwindAggregateOperatorsQueryInMemoryTest</c>
///         one for one. An aggregate over an empty subquery throws on InMemory instead of yielding a default, and a local <c>IEnumerable</c> in <c>Contains</c> has no translation.
///     </para>
///     <para>
///         These are <strong>backing-store</strong> limitations, not InfoCarrier gaps — a local
///         InMemory provider behaves identically with no wire involved — so the overrides assert
///         that behavior rather than suppress the test. They do not apply to the relational
///         (SQLite) backend of ADR-009 Tier B and must be deleted, not carried over, when it lands.
///     </para>
/// </remarks>
public class NorthwindAggregateOperatorsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindAggregateOperatorsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override async Task Average_no_data_subquery(bool async)
        => Assert.Equal(
            "Sequence contains no elements",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Average_no_data_subquery(async))).Message);

    /// <inheritdoc />
    public override async Task Max_no_data_subquery(bool async)
        => Assert.Equal(
            "Sequence contains no elements",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Max_no_data_subquery(async))).Message);

    /// <inheritdoc />
    public override async Task Min_no_data_subquery(bool async)
        => Assert.Equal(
            "Sequence contains no elements",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Min_no_data_subquery(async))).Message);

    /// <inheritdoc />
    public override async Task Average_on_nav_subquery_in_projection(bool async)
        => Assert.Equal(
            "Sequence contains no elements",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Average_on_nav_subquery_in_projection(async))).Message);

    /// <inheritdoc />
    public override async Task Sum_over_scalar_returning_subquery(bool async)
        => Assert.Equal(
            "Nullable object must have a value.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Sum_over_scalar_returning_subquery(async))).Message);

    /// <inheritdoc />
    public override Task Collection_Last_member_access_in_projection_translated(bool async)
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Collection_Last_member_access_in_projection_translated(async));

    /// <inheritdoc />
    public override async Task Contains_with_local_enumerable_inline(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await base.Contains_with_local_enumerable_inline(async));

    /// <inheritdoc />
    public override async Task Contains_with_local_enumerable_inline_closure_mix(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await base.Contains_with_local_enumerable_inline_closure_mix(async));
}
