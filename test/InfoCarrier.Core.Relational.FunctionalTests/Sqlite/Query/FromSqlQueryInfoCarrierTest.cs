// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>FromSqlQueryTestBase</c> on ADR-009 <b>Tier B</b> (#60) - the first spec base this
///     provider could not host at all until raw SQL was gated in and switched on.
/// </summary>
/// <remarks>
///     <para>
///         <b>126 of 148 on the first run, and the four overrides below were written after it.</b>
///         EF's own <c>FromSqlQuerySqliteTest</c> carries seven; three of those are
///         <c>AssertSql</c> baselines that have no meaning here, because the client emits no SQL to
///         assert on.
///     </para>
///     <para>
///         <b>The first run was 94 of 148, and what closed the gap was the type allowlist rather
///         than anything about SQL.</b> Thirty-two of the fifty-four reds were
///         <c>Type 'Microsoft.Data.Sqlite.SqliteParameter' is not on the deserialization
///         allowlist</c>. A <c>DbParameter</c> passed to <c>FromSqlRaw</c> is an ordinary object
///         with a parameterless constructor and settable properties, so the wire walks it and the
///         server rebuilds it without special handling - it was refused only because ADR-008
///         constraint 2 refuses every type the model does not imply. R85's seam is what admits it,
///         and the harness admits the store's own parameter type alongside the raw-SQL grant
///         (<c>InfoCarrierBackendTestStore.StoreParameterType</c>). <b>This is the fourth time in
///         this issue that "the allowlist decided the behaviour" turned out to be the whole
///         story</b> - R84, R89, R91 and now this.
///     </para>
///     <para>
///         <b>The fourteen still red are classified and none of them is a wire defect.</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <b>6 <c>Bad_data_error_handling_null*</c></b> - the server answers
///                 <c>The required column 'CategoryID' was not present</c> where the base expects
///                 <c>An error occurred while reading a database value</c>. A consequence of this
///                 tier's own harness: <c>NorthwindInfoCarrierSqliteServerContext</c> maps
///                 <c>Product.CategoryID</c>, which the core <c>NorthwindContext</c> ignores,
///                 because this tier builds its store from the model and the <c>ProductView</c>
///                 query needs the column to exist. The base's SQL selects a subset of columns, so
///                 the extra required column is missed before any value is read. Same note, same
///                 cause, as the <c>ProductView</c> paragraph in that file.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>4 <c>Include_*_connection*</c></b> - both reach for the client's own
///                 <c>DbConnection</c> to open or close it, and
///                 <see cref="RelationalInfoCarrierTestStore.Connection" /> refuses one by design:
///                 a test that reached past the wire to the database would say nothing about this
///                 provider. ADR-013's amendment names this half of <c>RelationalTestStore</c> as
///                 the half that must stay refused.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>2 <c>FromSqlRaw_queryable_simple_projection_composed</c></b> - the base's own
///                 body casts the client's type mapping to <c>RelationalTypeMapping</c>. That is
///                 ADR-013's gate question answered inside a test rather than in a fixture, and
///                 there is nothing to do about it short of making the client relational.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>2 <c>Multiple_occurrences_of_FromSql_with_db_parameter_adds_two_parameters</c></b>
///                 - <c>Database.SqlQueryRaw</c>, which is a separate entry point rather than a
///                 query root (<c>architecture.md</c> section 6a <b>D8</b> item 2) and is untouched.
///             </description>
///         </item>
///     </list>
/// </remarks>
public class FromSqlQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : FromSqlQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     The store's own parameter type, as EF's SQLite class supplies. Whether one survives the
    ///     wire was the open question when this base was adopted; it does, once the allowlist
    ///     admits it. See the class remarks.
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
