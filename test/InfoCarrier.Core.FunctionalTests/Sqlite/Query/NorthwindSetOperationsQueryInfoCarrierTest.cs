// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

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
///         The private client-projection helper remains local, outside the owner's shared public
///         helper parity goal. Its measured answer is attributed, while own tests pin that behavior
///         and registered public equivalents exercise the actual backend's decision.
///     </para>
/// </remarks>
public class NorthwindSetOperationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindSetOperationsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindSetOperationsQuerySqliteTest.cs", 20, 24,
        Justification = "Client evaluation in projection. Issue #16243.")]
    [InfoCarrierDesign(8,
        Justification = "The upstream helper is private and cannot be admitted on the server. Public shared helpers require registration on both ends.",
        Deviation = DeviationKind.AnswerNotRefusal,
        DeviationNote = "The private projection and UNION run locally over materialized rows; own tests pin this behavior.")]
    public override Task Client_eval_Union_FirstOrDefault(bool async)
        => base.Client_eval_Union_FirstOrDefault(async);
}
