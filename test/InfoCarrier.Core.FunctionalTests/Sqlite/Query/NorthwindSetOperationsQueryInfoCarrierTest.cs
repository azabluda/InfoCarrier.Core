// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NorthwindSetOperationsQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A.</b> The class it replaces added nothing at all: it inherited the
///         core base and declared an empty body. What the move buys is the relational base's three
///         members, two of which assert that the provider <em>refuses</em> a query.
///     </para>
///     <para>
///         The client-projection UNION override adopts SQLite's refusal after H43 reproduced
///         and fixed the client-side answer. The other relational refusal tests stay inherited.
///     </para>
/// </remarks>
public class NorthwindSetOperationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindSetOperationsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSetOperationsQuerySqliteTest.cs", 20, 24,
        Justification = "Client evaluation in projection. Issue #16243.")]
    public override async Task Client_eval_Union_FirstOrDefault(bool async)
        => Assert.Equal(
            RelationalStrings.SetOperationsNotAllowedAfterClientEvaluation,
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_eval_Union_FirstOrDefault(async))).Message);
}
