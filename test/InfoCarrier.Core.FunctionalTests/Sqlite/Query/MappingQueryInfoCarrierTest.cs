// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>MappingQueryTestBase</c> on ADR-009 <b>Tier B</b> — a cut-down Northwind model whose
///     tables and columns are renamed after the fact, asking whether a mapping the client rewrote
///     still addresses the server's store.
/// </summary>
/// <remarks>
///     <para>
///         <b>The blocker was the store's NAME, and it cost a seed rather than an API.</b> The
///         base's fixture supplies a model and <em>no seed at all</em>, because EF's providers
///         hand it a prebuilt <c>northwind.db</c> through <c>SqliteNorthwindTestStoreFactory</c>.
///         This tier has no curated file: it builds each store from whichever model reaches it
///         first. Leaving <c>StoreName</c> at its inherited <c>"Northwind"</c> would therefore
///         have initialized the shared <c>Northwind.db</c> from this three-table model and broken
///         every Northwind class in the suite — the store-lifetime coupling
///         <c>SqliteInfoCarrierBackendTestStore</c> exists to prevent. R62 declined to probe it
///         for exactly that reason.
///     </para>
///     <para>
///         <b>The store is renamed and seeded here instead</b>, from
///         <see cref="NorthwindData" />'s own <c>Create*</c> arrays, so the 91 customers, 9
///         employees and 830 orders the base asserts are the real rows rather than invented ones.
///         Only the three properties this model keeps are written; every other one is
///         <c>Ignore</c>d by the base and has no column to write to.
///     </para>
///     <para>
///         <b>EF's four <c>MappingQuerySqliteTest</c> overrides are deliberately NOT adopted.</b>
///         Each asserts a SQL string against <c>Fixture.TestSqlLoggerFactory.Sql</c>, and that
///         property observes the <em>client's</em> log — a client with no database, which emits no
///         SQL (R54). They are #56's "SQL plumbing only" group. The core base's own four tests
///         assert results, and those are the adoptable part.
///     </para>
/// </remarks>
public class MappingQueryInfoCarrierTest(MappingQueryInfoCarrierTest.MappingQueryInfoCarrierFixture fixture)
    : MappingQueryTestBase<MappingQueryInfoCarrierTest.MappingQueryInfoCarrierFixture>(fixture)
{
    public class MappingQueryInfoCarrierFixture : MappingQueryFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <summary>
        ///     A store of its own, and this is the whole reason the base could not be adopted
        ///     before.
        /// </summary>
        /// <remarks>
        ///     The inherited value is <c>"Northwind"</c>, which on this tier is the same file the
        ///     Northwind query fixtures share.
        /// </remarks>
        protected override string StoreName
            => "MappingQuery";

        /// <inheritdoc />
        protected override string DatabaseSchema
            => null!;

        /// <inheritdoc />
        /// <remarks>
        ///     EF's <c>MappingQuerySqliteFixture</c>'s, verbatim: the base maps
        ///     <c>MappedCustomer</c> to table <c>Broken</c> and column <c>Broken</c> on purpose,
        ///     and expects the provider's fixture to correct both.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<MappedCustomer>(e =>
            {
                e.Property(c => c.CompanyName2).Metadata.SetColumnName("CompanyName");
                e.Metadata.SetTableName("Customers");
            });
        }

        /// <summary>
        ///     Seeds the three renamed tables from <see cref="NorthwindData" />.
        /// </summary>
        /// <remarks>
        ///     The base seeds nothing because EF's stores arrive prebuilt. Every property the
        ///     model does not keep is <c>Ignore</c>d, so there is nothing else to copy across:
        ///     the store this creates holds <c>Customers(CustomerID, CompanyName)</c>,
        ///     <c>Employees(EmployeeID, City)</c> and <c>Orders(OrderID, ShipVia)</c>, which is
        ///     exactly what the four tests read.
        /// </remarks>
        protected override async Task SeedAsync(PoolableDbContext context)
        {
            context.AddRange(
                NorthwindData.CreateCustomers().Select(
                    c => new MappedCustomer { CustomerID = c.CustomerID, CompanyName2 = c.CompanyName }));

            context.AddRange(
                NorthwindData.CreateEmployees().Select(
                    e => new MappedEmployee { EmployeeID = e.EmployeeID, City2 = e.City }));

            context.AddRange(
                NorthwindData.CreateOrders().Select(
                    o => new MappedOrder { OrderID = o.OrderID, ShipVia2 = (ShipVia?)o.ShipVia }));

            await context.SaveChangesAsync();
        }
    }
}
