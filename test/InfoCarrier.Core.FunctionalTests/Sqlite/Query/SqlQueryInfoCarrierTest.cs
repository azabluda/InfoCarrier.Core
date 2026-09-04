// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>SqlQueryTestBase</c> on ADR-009 <b>Tier B</b> (#56) — the ad-hoc-entity half of
///     <c>Database.SqlQuery&lt;T&gt;</c>, and the last specification base with no subclass
///     anywhere.
/// </summary>
/// <remarks>
///     <para>
///         <b>The scalar half was adopted first, and this is the other one.</b>
///         <see cref="NorthwindSqlQueryInfoCarrierTest" /> covers
///         <c>NorthwindSqlQueryTestBase</c>, whose four methods all project into a primitive; this
///         base projects into <c>UnmappedCustomer</c>, <c>UnmappedOrder</c>,
///         <c>UnmappedProduct</c> and <c>UnmappedEmployee</c>, which take EF down its ad-hoc
///         entity-type path. Both halves ride the same wire node, <c>SqlQueryRootStubNode</c>, and
///         the same raw-SQL grant the fixture already carries
///         (<c>arbitrarySqlExecution: true</c>, which also turns on
///         <c>AddInfoCarrierRelationalClient()</c> and admits the store's parameter type).
///     </para>
///     <para>
///         <b>ADR-013's gate question is answered in the negative, and it was read rather than
///         assumed.</b> The base's 1301 lines contain no <c>UseTransaction</c>, no
///         <c>GetDbTransaction()</c> and no <c>ExecuteWithStrategyInTransactionAsync</c>. Its one
///         abstract member is <c>CreateDbParameter</c>, and every route to a context runs through
///         <c>Fixture.CreateContext()</c>. Nothing in it requires the client to be relational.
///     </para>
///     <para>
///         <b>The four <c>Bad_data_error_handling_invalid_cast*</c> overrides are EF's own.</b>
///         <c>SqlQuerySqliteTest</c> disables them because SQLite is dynamically typed, so there
///         is no invalid cast to make; adopting them here is convergence with the reference
///         provider rather than a workaround of ours. EF's remaining overrides are
///         <c>AssertSql</c> baselines, which have no meaning on this side of the wire because the
///         client emits no SQL — so those tests are left to the base, exactly as
///         <see cref="FromSqlQueryInfoCarrierTest" /> leaves them.
///     </para>
/// </remarks>
public class SqlQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : SqlQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     The store's own parameter type, as EF's SQLite class supplies and as
    ///     <see cref="FromSqlQueryInfoCarrierTest" /> already does. It crosses the wire because the
    ///     harness admits it alongside the raw-SQL grant (R85's seam).
    /// </remarks>
    protected override DbParameter CreateDbParameter(string name, object value)
        => new SqliteParameter { ParameterName = name, Value = value };

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite is dynamically typed, so there is no invalid cast to make.</remarks>
    public override Task Bad_data_error_handling_invalid_cast_key(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite is dynamically typed, so there is no invalid cast to make.</remarks>
    public override Task Bad_data_error_handling_invalid_cast(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite is dynamically typed, so there is no invalid cast to make.</remarks>
    public override Task Bad_data_error_handling_invalid_cast_projection(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite is dynamically typed, so there is no invalid cast to make.</remarks>
    public override Task Bad_data_error_handling_invalid_cast_no_tracking(bool async)
        => Task.CompletedTask;
}
