// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The Northwind query fixture on ADR-009 Tier B — the client remotes to a server running
///     against SQLite rather than InMemory.
/// </summary>
/// <remarks>
///     Tier A's provider client-evaluates nearly everything, so a query failing there may be
///     failing because InMemory cannot do it rather than because this provider is wrong. Running
///     the same inherited bases against a backend that genuinely translates is what separates
///     the two, and it is what turns the InMemory-limitation overrides from an assumption into a
///     measurement (roadmap M3).
/// </remarks>
/// <typeparam name="TModelCustomizer">The model customizer.</typeparam>
public class NorthwindQueryInfoCarrierSqliteFixture<TModelCustomizer>
    : NorthwindQueryRelationalFixture<TModelCustomizer>
    where TModelCustomizer : ITestModelCustomizer, new()
{
    private ITestStoreFactory? _infoCarrierTestStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _infoCarrierTestStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            copyDbContextParameters: (client, server) =>
                CopyDbContextParameters((NorthwindContext)client, (NorthwindContext)server),
            // EF wraps a failed column read into `ErrorMaterializingProperty*` ONLY when detailed
            // errors are on -- the try/catch around the read is emitted by
            // `ShaperProcessingExpressionVisitor` under `if (_detailedErrorsEnabled ...)`. Here the
            // read happens on the SERVER, and a shared fixture's own `AddOptions` deliberately does
            // not reach it (A29), so the server had detailed errors off and a raw
            // `SqliteException: The data is NULL at ordinal 5` crossed where EF's own message was
            // expected. This is the server half of what the fixture already asks of the client.
            onAddOptions: b => b.EnableDetailedErrors(),
            serverContextType: typeof(NorthwindInfoCarrierSqliteServerContext),
            configureConventions: ConfigureConventions,
            relationalClientStore: true,
            arbitrarySqlExecution: true,
            allowedTypes: AdHocProjectionTypes);

    /// <summary>
    ///     The projection types <c>SqlQueryTestBase</c> names, declared as an application must
    ///     declare them (ADR-008 constraint 2).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b><c>Database.SqlQuery&lt;T&gt;</c> into an unmapped type is the one query root
    ///         whose type the model cannot imply.</b> EF builds an <em>ad-hoc</em> entity type for
    ///         it, which does not appear in <c>IModel.GetEntityTypes()</c>, so
    ///         <c>TypeAllowlist.ForModel</c> has nothing to infer from and the boundary refuses the
    ///         root — 90 of <c>SqlQueryInfoCarrierTest</c>'s 119 tests, measured before this list
    ///         existed. <c>SqlQueryRaw_queryable_simple_mapped_type</c> passed in the same run,
    ///         which is the control: <c>CustomerQuery</c> IS in the model.
    ///     </para>
    ///     <para>
    ///         <b>This is the product's own route and not a workaround.</b> An application that
    ///         projects into a DTO calls <c>AllowTypes</c> on the client and
    ///         <c>AddInfoCarrierAllowedTypes</c> on the server; the harness is an application and
    ///         does the same. Nothing is skipped and no assertion is weakened.
    ///     </para>
    ///     <para>
    ///         Declared on the shared Northwind fixture, so every Tier B Northwind class carries
    ///         them. That widens nothing for a class that never names one: the allowlist decides
    ///         what a payload MAY name, and a query that names none is unaffected.
    ///     </para>
    /// </remarks>
    private static Type[] AdHocProjectionTypes =>
    [
        typeof(UnmappedCustomer),
        typeof(UnmappedOrder),
        typeof(UnmappedProduct),
        typeof(UnmappedEmployee),
    ];

    /// <summary>
    ///     Snaps <c>OrderDetail.Discount</c> back to its two-decimal value after seeding.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Discount</c> is a <see cref="float" />. <c>NorthwindData</c>'s <c>0.15f</c> lands
    ///         in SQLite's 8-byte <c>REAL</c> column as <c>0.150000005960464…</c> — the 32-bit
    ///         value shown at 64-bit width — where EF Core's own SQLite suite reads a clean
    ///         <c>0.15</c> because it uses a prebuilt <c>northwind.db</c> rather than seeding from
    ///         the model (this tier has to build its store from the model; see
    ///         <see cref="NorthwindInfoCarrierSqliteServerContext" />).
    ///     </para>
    ///     <para>
    ///         That widening gives <c>ef_sum(CAST("Discount" AS TEXT))</c> a per-row residual, and
    ///         <c>NorthwindAggregateOperatorsQueryInfoCarrierTest.Type_casting_inside_sum</c>
    ///         (sync + async) sums the whole table, so it differs from EF's expected <c>121.040</c>
    ///         by about 1.8e-6. <c>round(x, 2)</c> over every row restores the values EF's curated
    ///         store holds; every Northwind discount is a two-decimal number, so the already-exact
    ///         rows (<c>0</c>, <c>0.25</c>) are rewritten with themselves.
    ///     </para>
    /// </remarks>
    protected override async Task SeedAsync(NorthwindContext context)
    {
        await base.SeedAsync(context);

        await AddTheOrderColumnsTheModelIgnoresAsync(context);

        // The table name is `NorthwindInfoCarrierSqliteServerContext`'s, which maps `OrderDetail`
        // to "Order Details" so the relational spec bases can write that name into their own raw
        // SQL. This statement is the only other place the name is written by hand, and R97 moved
        // one without the other.
        await context.Database.ExecuteSqlRawAsync(
            """UPDATE "Order Details" SET "Discount" = round("Discount", 2)""");
    }

    /// <summary>
    ///     Adds the ten <c>Orders</c> columns the Northwind model ignores, straight to the store.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Outside the model on purpose, and that is the whole point of this method.</b> The
    ///         core <c>NorthwindContext</c> ignores <c>Freight</c>, <c>RequiredDate</c>,
    ///         <c>ShippedDate</c>, <c>ShipVia</c>, <c>ShipName</c>, <c>ShipAddress</c>,
    ///         <c>ShipCity</c>, <c>ShipRegion</c>, <c>ShipPostalCode</c> and <c>ShipCountry</c>,
    ///         while the real Northwind schema has all ten. EF Core's own SQLite suite gets both at
    ///         once because its store is a prebuilt <c>northwind.db</c>: the columns exist and the
    ///         model does not know them. This tier builds its store from the model, so it could
    ///         previously have one or the other.
    ///     </para>
    ///     <para>
    ///         <b>Both are needed, by tests that contradict each other through the model.</b>
    ///         <c>SqlQueryTestBase</c> reads <c>Orders</c> with raw SQL into its own
    ///         <c>UnmappedOrder</c>, which names nine of the ten, and failed with
    ///         <c>no such column: m.Freight</c> -- <c>Freight</c> only because it is the first one
    ///         the parser reaches. Meanwhile
    ///         <c>Average_with_unmapped_property_access_throws_meaningful_exception</c> averages
    ///         <c>Order.ShipVia</c> and requires <c>QueryUnableToTranslateMember</c>, and
    ///         <c>Collection_select_nav_prop_all_client</c> and its sibling read
    ///         <c>ShipCity</c> the same way.
    ///     </para>
    ///     <para>
    ///         <b>MAPPING THEM ON THE SERVER MODEL FIXES THE FIRST TEN AND BREAKS THOSE EIGHT, and
    ///         that was measured rather than reasoned about.</b> This repository is two models: with
    ///         the property mapped on the server, the server answers a member access the CLIENT's
    ///         model calls unmapped, so a query EF refuses returns data instead. Shadow properties
    ///         do not avoid it -- <c>Property&lt;int?&gt;("ShipVia")</c> binds to the CLR member
    ///         whose name it matches, ignored or not. Raw DDL is the only route that gives the
    ///         store the column without giving either model the property, which is exactly the
    ///         state EF's own store is in.
    ///     </para>
    ///     <para>
    ///         Every column is nullable and nothing seeds them, so every row holds null.
    ///         <c>NorthwindData</c> never carried these values either, and
    ///         <c>SqlQueryTestBase.AssertUnmappedOrders</c> compares two results read from the SAME
    ///         store -- it asserts that the wire round-trips what is there, not what Northwind
    ///         historically held.
    ///     </para>
    /// </remarks>
    private static async Task AddTheOrderColumnsTheModelIgnoresAsync(NorthwindContext context)
    {
        // Whole statements as literals, because `ExecuteSqlRawAsync` refuses both an interpolated
        // argument (EF1002) and a concatenated one (EF1003), and neither suppression is worth the
        // ten lines it would save.
        foreach (string statement in (string[])
                 [
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""RequiredDate"" TEXT",
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""ShippedDate"" TEXT",
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""ShipVia"" INTEGER",
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""Freight"" TEXT",
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""ShipName"" TEXT",
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""ShipAddress"" TEXT",
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""ShipCity"" TEXT",
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""ShipRegion"" TEXT",
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""ShipPostalCode"" TEXT",
                     @"ALTER TABLE ""Orders"" ADD COLUMN ""ShipCountry"" TEXT",
                 ])
        {
            await context.Database.ExecuteSqlRawAsync(statement);
        }

        // The values come from the same objects the base just inserted, so the store holds what
        // EF's prebuilt northwind.db holds. `SqliteParameter` rather than a bare value, because a
        // null has to reach the provider as `DBNull` and EF's raw-SQL path refuses a plain
        // `DBNull.Value` with "no store type mapping for properties of type 'DBNull'".
        foreach (Order order in NorthwindData.CreateOrders())
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                UPDATE "Orders" SET "RequiredDate" = @RequiredDate, "ShippedDate" = @ShippedDate,
                    "ShipVia" = @ShipVia, "Freight" = @Freight, "ShipName" = @ShipName,
                    "ShipAddress" = @ShipAddress, "ShipCity" = @ShipCity,
                    "ShipRegion" = @ShipRegion, "ShipPostalCode" = @ShipPostalCode,
                    "ShipCountry" = @ShipCountry
                WHERE "OrderID" = @OrderID
                """,
                Parameter("RequiredDate", order.RequiredDate),
                Parameter("ShippedDate", order.ShippedDate),
                Parameter("ShipVia", order.ShipVia),
                Parameter("Freight", order.Freight),
                Parameter("ShipName", order.ShipName),
                Parameter("ShipAddress", order.ShipAddress),
                Parameter("ShipCity", order.ShipCity),
                Parameter("ShipRegion", order.ShipRegion),
                Parameter("ShipPostalCode", order.ShipPostalCode),
                Parameter("ShipCountry", order.ShipCountry),
                Parameter("OrderID", order.OrderID));
        }

        static SqliteParameter Parameter(string name, object? value)
            => new(name, value ?? DBNull.Value);
    }

    private static void CopyDbContextParameters(NorthwindContext client, NorthwindContext server)
        => server.TenantPrefix = client.TenantPrefix;
}
