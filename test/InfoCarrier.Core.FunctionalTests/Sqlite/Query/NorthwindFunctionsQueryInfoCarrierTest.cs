// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NorthwindFunctionsQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A.</b> The Tier A class was a bare adoption of the core base with no
///         overrides; the relational base adds none of its own beyond the relational query
///         asserter. A base belongs to exactly one tier (CLAUDE.md), and every function this base
///         exercises translates on a relational backend.
///     </para>
///     <para>
///         Deliberately <b>no overrides</b>. Whatever a real SQLite backend cannot translate is
///         what the run reports, and an override written before the run would be the assumption
///         rather than the measurement (CLAUDE.md). EF's own <c>NorthwindFunctionsQuerySqliteTest</c>
///         overrides four <c>Sum_over_round</c>/<c>Sum_over_truncate</c> members with
///         <c>AssertTranslationFailed</c>; those are adopted here only if this run shows the same.
///     </para>
/// </remarks>
public class NorthwindFunctionsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindFunctionsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);
