// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The <em>server-side</em> Northwind context for the relational tier: the shared model plus
///     SQL defining queries for the keyless entity types.
/// </summary>
/// <remarks>
///     The same role <see cref="NorthwindInfoCarrierServerContext" /> plays for Tier A, in the
///     dialect the backing store speaks. A defining query is how the store produces rows, which
///     is precisely the part of the model a remoting client has no business knowing — the client
///     needs the keyless types and nothing more.
/// </remarks>
public class NorthwindInfoCarrierSqliteServerContext(DbContextOptions options) : NorthwindContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CustomerQuery>().ToSqlQuery(
            """
            SELECT "c"."Address", "c"."City", "c"."CompanyName", "c"."ContactName", "c"."ContactTitle"
            FROM "Customers" AS "c"
            """);

        modelBuilder.Entity<OrderQuery>().ToSqlQuery(
            """
            SELECT "o"."CustomerID"
            FROM "Orders" AS "o"
            """);

        modelBuilder.Entity<ProductQuery>().ToSqlQuery(
            """
            SELECT "p"."ProductID", "p"."ProductName", 'Food' AS "CategoryName"
            FROM "Products" AS "p"
            WHERE "p"."Discontinued" = 0
            """);

        // The filter's search term is a per-instance value, so it cannot be baked into a static
        // SQL string the way the others are; it is compared in the query filter instead.
        modelBuilder.Entity<CustomerQueryWithQueryFilter>().ToSqlQuery(
            """
            SELECT "c"."CompanyName", (
                SELECT COUNT(*) FROM "Orders" AS "o" WHERE "o"."CustomerID" = "c"."CustomerID") AS "OrderCount"
            FROM "Customers" AS "c"
            """);
    }
}
