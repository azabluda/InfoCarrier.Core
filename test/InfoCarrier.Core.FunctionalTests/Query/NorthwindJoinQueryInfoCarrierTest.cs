// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <see cref="NorthwindJoinQueryTestBase{TFixture}" /> over the InfoCarrier client with an InMemory backend.
/// </summary>
/// <remarks>
///     <para>
///         Each override mirrors EF Core's own <c>NorthwindJoinQueryInMemoryTest</c>
///         one for one. InMemory cannot translate <c>RightJoin</c> or a join against a local collection, and joins between client-evaluated sources are unimplemented (EF issue #21200).
///     </para>
///     <para>
///         These are <strong>backing-store</strong> limitations, not InfoCarrier gaps — a local
///         InMemory provider behaves identically with no wire involved — so the overrides assert
///         that behavior rather than suppress the test. They do not apply to the relational
///         (SQLite) backend of ADR-009 Tier B and must be deleted, not carried over, when it lands.
///     </para>
/// </remarks>
public class NorthwindJoinQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindJoinQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override Task SelectMany_with_client_eval(bool async)
        // Joins between sources with client eval. Issue #21200.
        => Assert.ThrowsAsync<NotImplementedException>(() => base.SelectMany_with_client_eval(async));

    /// <inheritdoc />
    public override Task SelectMany_with_client_eval_with_collection_shaper(bool async)
        // Joins between sources with client eval. Issue #21200.
        => Assert.ThrowsAsync<NotImplementedException>(() => base.SelectMany_with_client_eval_with_collection_shaper(async));

    /// <inheritdoc />
    public override Task SelectMany_with_client_eval_with_collection_shaper_ignored(bool async)
        // Joins between sources with client eval. Issue #21200.
        => Assert.ThrowsAsync<NotImplementedException>(() => base.SelectMany_with_client_eval_with_collection_shaper_ignored(async));

    /// <inheritdoc />
    public override Task SelectMany_with_client_eval_with_constructor(bool async)
        // Joins between sources with client eval. Issue #21200.
        => Assert.ThrowsAsync<NotImplementedException>(() => base.SelectMany_with_client_eval_with_constructor(async));

    /// <inheritdoc />
    public override Task RightJoin(bool async)
        => AssertTranslationFailed(() => base.RightJoin(async));

    /// <inheritdoc />
    public override async Task Join_local_collection_int_closure_is_cached_correctly(bool async)
    {
        var ids = new uint[] { 1, 2 };

        await AssertTranslationFailed(() => AssertQueryScalar(
            async,
            ss => from e in ss.Set<Employee>()
                  join id in ids on e.EmployeeID equals id
                  select e.EmployeeID));

        ids = [3];
        await AssertTranslationFailed(() => AssertQueryScalar(
            async,
            ss => from e in ss.Set<Employee>()
                  join id in ids on e.EmployeeID equals id
                  select e.EmployeeID));
    }
}
