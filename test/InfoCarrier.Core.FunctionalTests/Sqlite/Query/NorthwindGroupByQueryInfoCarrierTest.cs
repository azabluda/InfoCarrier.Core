// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NorthwindGroupByQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A, and the move deletes six overrides.</b> Each Tier A override
///         asserted <c>InMemoryStrings.NonComposedGroupByNotSupported</c> — the InMemory provider
///         cannot translate a <c>GroupBy</c> that is not composed into an aggregate or a
///         projection of its elements. A relational provider translates all of them, and the
///         Tier A class already recorded that the overrides "must be deleted rather than carried
///         over" once a relational backend landed. The relational base adds none of its own.
///     </para>
///     <para>
///         Deliberately <b>no overrides</b>. EF's own <c>NorthwindGroupByQuerySqliteTest</c>
///         overrides twelve members with <c>SqliteStrings.ApplyNotSupported</c> — SQLite has no
///         correlated <c>APPLY</c> — plus one <c>SqliteException</c>; those are adopted here only
///         if this run shows the same refusal reaching the client.
///     </para>
/// </remarks>
public class NorthwindGroupByQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindGroupByQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);
