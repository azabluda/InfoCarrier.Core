// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The <em>server-side</em> Northwind context for the relational tier: the shared model plus
///     SQL defining queries for the keyless entity types.
/// </summary>
/// <remarks>
///     The same role <see cref="NorthwindInfoCarrierContext" /> plays for Tier A, in the
///     dialect the backing store speaks. A defining query is how the store produces rows, which
///     is precisely the part of the model a remoting client has no business knowing — the client
///     needs the keyless types and nothing more.
/// </remarks>
public class NorthwindInfoCarrierSqliteContext(DbContextOptions options) : NorthwindContext(options)
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

        // `Product.CategoryID` USED TO BE MAPPED HERE, so that the `ProductView` query below had a
        // column to read, and mapping it cost four tests. `FromSqlQueryTestBase`'s
        // `Bad_data_error_handling_null` pair writes raw SQL naming the six columns EF's own model
        // maps, and a mapped `CategoryID` puts a seventh in the shaper's required list, so the
        // query died on "The required column 'CategoryID' was not present" instead of reaching the
        // null EF asserts. The column now arrives as raw DDL in
        // `NorthwindQueryInfoCarrierSqliteFixture.AddTheColumnsTheModelIgnoresAsync`, which is
        // where the same problem was already solved for ten `Orders` columns and twelve on
        // `Employees` -- the store gets the column, neither model gets the property, and that is
        // exactly the state EF's own prebuilt `northwind.db` is in.

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

        // `NorthwindRelationalContext` maps this one `ToTable("Order Details")`, and the relational
        // spec bases write that name into raw SQL by hand -- `NorthwindBulkUpdatesRelationalTestBase
        // .Delete_FromSql_converted_to_subquery` has `FROM [Order Details]` in its source. This
        // tier's server derives from the CORE `NorthwindContext`, where the table is `OrderDetails`,
        // and builds its store from that model, so the base's SQL found no table. Its sibling
        // `Update_FromSql_set_constant` passed untouched because it names `[Customers]`, which both
        // models spell the same way -- which is what makes this a HARNESS mismatch and not anything
        // about the wire.
        //
        // Server-side only, like everything else here: a table name is the store's business and no
        // part of it crosses. Same shape as the `ProductView` and `Product.CategoryID` notes above.
        //
        // THE ONE OTHER PLACE THIS NAME IS WRITTEN BY HAND IS
        // `NorthwindQueryInfoCarrierSqliteFixture.SeedAsync`, and R97 changed this line without
        // that one: the seed's `UPDATE "OrderDetails"` then threw inside the shared store's
        // initialization and took 236 tests in the classes sharing "Northwind" with it. Both are
        // needed or neither.
        modelBuilder.Entity<OrderDetail>().ToTable("Order Details");

        modelBuilder.Entity<CustomerQueryWithQueryFilter>().ToSqlQuery(
            """
            SELECT "c"."CompanyName", (
                SELECT COUNT(*) FROM "Orders" AS "o" WHERE "o"."CustomerID" = "c"."CustomerID") AS "OrderCount"
            FROM "Customers" AS "c"
            """);
    }
}
