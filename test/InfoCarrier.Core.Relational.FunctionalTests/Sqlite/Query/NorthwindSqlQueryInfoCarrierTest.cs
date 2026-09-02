// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NorthwindSqlQueryTestBase</c> on ADR-009 <b>Tier B</b> (#56) — the scalar half of
///     <c>Database.SqlQuery&lt;T&gt;</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>All four of its methods are scalar</b>, which is why this base and not
///         <c>SqlQueryTestBase</c> is the one that adopts first. <c>SqlQueryRaw&lt;int&gt;</c> and
///         <c>SqlQuery&lt;int&gt;</c> make EF build a <c>SqlQueryRootExpression</c>, and
///         <c>SqlQueryRootStubNode</c> is what carries it. The other base's tests project into
///         <c>UnmappedProduct</c> and <c>UnmappedCustomer</c>, which take EF down its ad-hoc
///         entity-type path instead — a different problem, priced separately.
///     </para>
///     <para>
///         <b>Two things had to be true before this class could exist, and neither is about SQL.</b>
///         The client had to answer <c>IRelationalDatabaseFacadeDependencies</c>, which R114
///         registers from outside <c>InfoCarrier.Core</c> so that D3 stands; and the scalar root
///         needed a wire node of its own, because it is the one query root with <em>no entity
///         type</em> for the server to resolve.
///     </para>
///     <para>
///         <b>No override is taken from EF's <c>NorthwindSqlQuerySqliteTest</c>.</b> That class
///         adds only <c>CreateDbParameter</c> and a private <c>AssertSql</c> helper; the baselines
///         it would assert have no meaning here, because the client emits no SQL.
///     </para>
/// </remarks>
public class NorthwindSqlQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindSqlQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     The store's own parameter type, as EF's SQLite class supplies and as
    ///     <see cref="FromSqlQueryInfoCarrierTest" /> already does. It crosses the wire because the
    ///     harness admits it alongside the raw-SQL grant (R85's seam).
    /// </remarks>
    protected override DbParameter CreateDbParameter(string name, object value)
        => new SqliteParameter { ParameterName = name, Value = value };
}
