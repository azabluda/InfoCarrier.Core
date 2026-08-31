// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>WarningsTestBase</c> on ADR-009 <b>Tier B</b>, mirroring EF's own
///     <c>WarningsSqliteTest</c>, which is also a bare class over the shared Northwind fixture.
/// </summary>
/// <remarks>
///     <para>
///         What it covers is the diagnostics a query raises rather than its answer: paging without
///         an <c>OrderBy</c>, a first-or-default that could match several rows, and the events EF
///         logs for each. <b>Those warnings are raised by the query pipeline, which on this
///         provider runs on the server</b>, so the base is a direct check that a server-side
///         diagnostic still reaches a client that has no database.
///     </para>
///     <para>
///         <b>R39 is what made it adoptable, and the obstacle was the fixture rather than the
///         base.</b> <c>WarningsTestBase</c> constrains <c>TFixture</c> to
///         <c>NorthwindQueryRelationalFixture</c>, which declares
///         <c>public new RelationalTestStore TestStore => (RelationalTestStore)base.TestStore</c> —
///         and <see cref="InfoCarrierTestStore" /> is a <c>TestStore</c>, not a
///         <c>RelationalTestStore</c>. <b>ADR-013's 2026-08-30 amendment is the rule that decides
///         it</b>: a cast like that blocks a base only when every route runs through it. No test
///         in this class reads <c>Fixture.TestStore</c>, and re-parenting the shared fixture was
///         measured behaviour-neutral across all 2,470 Northwind Tier B tests before this class
///         was written.
///     </para>
/// </remarks>
public class WarningsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : WarningsTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);
