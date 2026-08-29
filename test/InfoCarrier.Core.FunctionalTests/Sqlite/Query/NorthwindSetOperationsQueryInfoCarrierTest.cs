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
///         Deliberately <b>no overrides</b>. Whether this provider refuses those two is the
///         question the class is here to answer, and an override written before the run would be
///         the assumption rather than the measurement.
///     </para>
/// </remarks>
public class NorthwindSetOperationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindSetOperationsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);
