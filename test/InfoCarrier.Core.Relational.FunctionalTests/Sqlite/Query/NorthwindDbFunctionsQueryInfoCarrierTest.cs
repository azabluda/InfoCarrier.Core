// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NorthwindDbFunctionsQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> — the first
///     coverage this repository has of <c>EF.Functions</c> at all.
/// </summary>
/// <remarks>
///     <para>
///         <b>It needs no relational client store, and believing otherwise cost a whole mechanism
///         (R77, reverted).</b> The base constrains its fixture to
///         <c>NorthwindQueryRelationalFixture</c>, which declares
///         <c>public new RelationalTestStore TestStore =&gt; (RelationalTestStore)base.TestStore;</c>
///         — and the inference that the constraint therefore forces the cast is simply wrong. A
///         property is evaluated when something reads it, and no test here reads it. Measured both
///         ways: <b>30 tests, 24 green, 6 red, byte-identical</b> with the relational shell and
///         without it. <b>A type constraint names what a fixture must BE, not what a test will
///         TOUCH</b>, and that is the reusable part.
///     </para>
///     <para>
///         <b>Nothing here is raw SQL</b>, which is what separates it from the other relational
///         bases still unadopted. <c>EF.Functions.Collate</c>, <c>Least</c> and <c>Greatest</c> are
///         ordinary method calls in the expression tree, so they cross the wire like any other node
///         and the server's SQLite provider translates them.
///     </para>
///     <para>
///         The collations are SQLite's own, as EF's <c>NorthwindDbFunctionsQuerySqliteTest</c> uses.
///         <b>No golden strings.</b> EF's SQLite class overrides most of these to assert SQL and
///         adds <c>Glob</c>; that is the provider's dialect, and this client emits no SQL at all.
///     </para>
/// </remarks>
public class NorthwindDbFunctionsQueryInfoCarrierTest(
    NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindDbFunctionsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    protected override string CaseInsensitiveCollation
        => "NOCASE";

    /// <inheritdoc />
    protected override string CaseSensitiveCollation
        => "BINARY";
}
