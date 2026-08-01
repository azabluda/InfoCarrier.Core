// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.InMemory.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <see cref="NorthwindSetOperationsQueryTestBase{TFixture}" /> over the InfoCarrier client with an InMemory backend.
/// </summary>
/// <remarks>
///     <para>
///         Each override mirrors EF Core's own <c>NorthwindSetOperationsQueryInMemoryTest</c>
///         one for one. InMemory refuses a set operation after a client-evaluated projection (EF issue #16243).
///     </para>
///     <para>
///         These are <strong>backing-store</strong> limitations, not InfoCarrier gaps — a local
///         InMemory provider behaves identically with no wire involved — so the overrides assert
///         that behavior rather than suppress the test. They do not apply to the relational
///         (SQLite) backend of ADR-009 Tier B and must be deleted, not carried over, when it lands.
///     </para>
/// </remarks>
public class NorthwindSetOperationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindSetOperationsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override async Task Collection_projection_before_set_operation_fails(bool async)
        // Client evaluation in projection. Issue #16243.
        => Assert.Equal(
            InMemoryStrings.SetOperationsNotAllowedAfterClientEvaluation,
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Collection_projection_before_set_operation_fails(async)))
            .Message);

    /// <inheritdoc />
    public override async Task Client_eval_Union_FirstOrDefault(bool async)
        // Client evaluation in projection. Issue #16243.
        => Assert.Equal(
            InMemoryStrings.SetOperationsNotAllowedAfterClientEvaluation,
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_eval_Union_FirstOrDefault(async))).Message);
}
