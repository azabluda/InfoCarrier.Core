// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <see cref="NorthwindKeylessEntitiesQueryTestBase{TFixture}" /> over the InfoCarrier client with an InMemory backend.
/// </summary>
/// <remarks>
///     <para>
///         Each override mirrors EF Core's own <c>NorthwindKeylessEntitiesQueryInMemoryTest</c>
///         one for one. InMemory has no database views, and cannot include a navigation from a keyless entity type.
///     </para>
///     <para>
///         These are <strong>backing-store</strong> limitations, not InfoCarrier gaps — a local
///         InMemory provider behaves identically with no wire involved — so the overrides assert
///         that behavior rather than suppress the test. They do not apply to the relational
///         (SQLite) backend of ADR-009 Tier B and must be deleted, not carried over, when it lands.
///     </para>
/// </remarks>
public class NorthwindKeylessEntitiesQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindKeylessEntitiesQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override Task KeylessEntity_by_database_view(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    public override async Task KeylessEntity_with_included_nav(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.KeylessEntity_with_included_nav(async));

    /// <inheritdoc />
    public override async Task KeylessEntity_with_included_navs_multi_level(bool async)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => base.KeylessEntity_with_included_navs_multi_level(async));
}
