// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Linq;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Northwind;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class NorthwindQueryInfoCarrierFixture<TModelCustomizer> : NorthwindQueryFixtureBase<TModelCustomizer>
        where TModelCustomizer : IModelCustomizer, new()
    {
        private ITestStoreFactory testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory =>
            InfoCarrierTestStoreFactory.EnsureInitialized(
                ref this.testStoreFactory,
                InfoCarrierTestStoreFactory.InMemory,
                this.ContextType,
                this.OnModelCreating,
                copyDbContextParameters: (c1, c2) => CopyDbContextParameters((NorthwindContext)c1, (NorthwindContext)c2));

        private static void CopyDbContextParameters(NorthwindContext clientDbContext, NorthwindContext backendDbContext)
        {
            backendDbContext.TenantPrefix = clientDbContext.TenantPrefix;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            var northwindContext = (NorthwindContext)context;

            modelBuilder.Entity<CustomerQuery>().ToInMemoryQuery(
                () => northwindContext.Customers.Select(
                    c => new CustomerQuery
                    {
                        Address = c.Address,
                        City = c.City,
                        CompanyName = c.CompanyName,
                        ContactName = c.ContactName,
                        ContactTitle = c.ContactTitle,
                    }));

            modelBuilder.Entity<OrderQuery>().ToInMemoryQuery(
                () => northwindContext.Orders.Select(o => new OrderQuery { CustomerID = o.CustomerID }));

            modelBuilder.Entity<ProductQuery>().ToInMemoryQuery(
                () => northwindContext.Products.Where(p => !p.Discontinued)
                    .Select(
                        p => new ProductQuery
                        {
                            ProductID = p.ProductID,
                            ProductName = p.ProductName,
                            CategoryName = "Food",
                        }));

            modelBuilder.Entity<CustomerQueryWithQueryFilter>().ToInMemoryQuery(
                () => northwindContext.Customers.Select(
                    c => new CustomerQueryWithQueryFilter
                    {
                        CompanyName = c.CompanyName,
                        OrderCount = c.Orders.Count(),
                        SearchTerm = northwindContext.SearchTerm,
                    }));
        }
    }
}
