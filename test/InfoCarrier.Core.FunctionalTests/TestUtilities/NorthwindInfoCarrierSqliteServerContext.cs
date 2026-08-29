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
        // `NorthwindContext` ignores `Product.CategoryID`, and EF's SQLite suite never notices
        // because its Northwind store is a prebuilt `northwind.db` holding the real schema. This
        // tier builds its store from the model, so the column has to exist for the view below to
        // have anything to read. Server-side only: the client model still ignores it, and a
        // property the client does not know about is skipped when the row is read back.
        modelBuilder.Entity<Product>().Property(p => p.CategoryID);

        // `NorthwindRelationalContext` maps this one `ToView("Alphabetical list of products")`,
        // and EF's SQLite suite passes because its Northwind store is a prebuilt `northwind.db`
        // that already holds the view. This tier builds its store from the model, so the view has
        // to be written out. `NorthwindData` derives `CategoryName` from a hard-coded map of
        // `CategoryID`, not from a `Categories` table, so a `CASE` reproduces it exactly and no
        // join is needed. Without it the set was empty and 69 rows were expected.
        modelBuilder.Entity<ProductView>().ToSqlQuery(
            """
            SELECT "p"."ProductID", "p"."ProductName", CASE "p"."CategoryID"
                WHEN 1 THEN 'Beverages'
                WHEN 2 THEN 'Condiments'
                WHEN 3 THEN 'Confections'
                WHEN 4 THEN 'Dairy Products'
                WHEN 5 THEN 'Grains/Cereals'
                WHEN 6 THEN 'Meat/Poultry'
                WHEN 7 THEN 'Produce'
                WHEN 8 THEN 'Seafood'
            END AS "CategoryName"
            FROM "Products" AS "p"
            WHERE "p"."Discontinued" = 0
            """);

        modelBuilder.Entity<CustomerQueryWithQueryFilter>().ToSqlQuery(
            """
            SELECT "c"."CompanyName", (
                SELECT COUNT(*) FROM "Orders" AS "o" WHERE "o"."CustomerID" = "c"."CustomerID") AS "OrderCount"
            FROM "Customers" AS "c"
            """);
    }
}
