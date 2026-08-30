// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

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
///         Deliberately <b>no overrides</b> beyond the relational base's. EF's own
///         <c>NorthwindAggregateOperatorsQuerySqliteTest</c> adds a handful of behaviour overrides
///         (an <c>ApplyNotSupported</c> refusal, two anonymous/tuple-array <c>Contains</c>
///         translation failures); those are adopted here only if this run shows the same.
///     </para>
/// </remarks>
public class NorthwindAggregateOperatorsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindAggregateOperatorsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);
