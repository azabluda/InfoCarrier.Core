// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NorthwindKeylessEntitiesQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A, and the move deletes three overrides.</b> One of them,
///         <c>KeylessEntity_by_database_view</c>, returned <c>Task.CompletedTask</c> outright,
///         because InMemory has no views; the other two asserted a throw where InMemory cannot
///         include a navigation from a keyless type. The SQLite server context already defines all
///         four keyless types through <c>ToSqlQuery</c>, so the store side needs nothing new.
///     </para>
///     <para>
///         The relational base adds two tests of its own and overrides two more, all four asserting
///         that the provider refuses a correlated collection it cannot identify. Left unoverridden
///         for the same reason as its siblings: the run is what answers that.
///     </para>
/// </remarks>
public class NorthwindKeylessEntitiesQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindKeylessEntitiesQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     EF's own <c>NorthwindKeylessEntitiesQuerySqliteTest</c> asserts a <c>SqliteException</c>
    ///     here and cites EF issue #21627, a `FromSql` mapping defect. The store refuses it the
    ///     same way through this wire, arriving wrapped, so the assertion keeps the engine's own
    ///     type name and message.
    /// </remarks>
    public override async Task KeylessEntity_with_nav_defining_query(bool async)
    {
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(
            () => base.KeylessEntity_with_nav_defining_query(async));

        Assert.Equal(typeof(SqliteException).FullName, exception.ServerExceptionTypeName);
        Assert.Contains("no such column", exception.Message);
    }
}
