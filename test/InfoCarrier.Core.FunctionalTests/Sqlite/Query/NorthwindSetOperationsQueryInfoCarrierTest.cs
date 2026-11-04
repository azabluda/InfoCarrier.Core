// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
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
///         The private projection helper cannot reach the server. Version 11 refuses the local
///         union before database execution; registered public equivalents retain backend behavior.
///     </para>
/// </remarks>
public class NorthwindSetOperationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindSetOperationsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSetOperationsQuerySqliteTest.cs", 20, 24,
        Justification = "Client evaluation in projection. Issue #16243.")]
    [InfoCarrierDesign(10,
        Justification = "Version 11 rejects implicit local equality operations after safe projection rewrites.",
        Deviation = DeviationKind.RefusedEarlier,
        DeviationNote = "The private upstream helper cannot be registered. Local union is refused during client planning rather than backend translation.")]
    public override async Task Client_eval_Union_FirstOrDefault(bool async)
        => Assert.Contains("implicit client execution of 'Union'",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_eval_Union_FirstOrDefault(async))).Message,
            StringComparison.Ordinal);
}
