// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using NorthwindCustomer = Microsoft.EntityFrameworkCore.TestModels.Northwind.Customer;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Query;

public class NorthwindFixture<TModelCustomizer> : NorthwindQueryFixtureBase<TModelCustomizer>
    where TModelCustomizer : ITestModelCustomizer, new()
{
    private ITestStoreFactory? _factory;

    protected override bool UsePooling => false;

    public async Task NoSyncTest(bool async, Func<bool, Task> test)
    {
        if (async)
        {
            await test(true);
        }
        else
        {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => test(false));
            Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", error.Message);
        }
    }

    protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
        new CosmosTier(), ContextType, OnModelCreating,
        copyDbContextParameters: (client, server) =>
            ((NorthwindContext)server).TenantPrefix = ((NorthwindContext)client).TenantPrefix,
        configureConventions: ConfigureConventions,
        configureServer: options => options.WithStoresOwnedDataInsideOwner(true));

    protected override bool ShouldLogCategory(string logCategory)
        => logCategory == DbLoggerCategory.Query.Name;

    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base.AddOptions(builder.ConfigureWarnings(warnings => warnings.Ignore(CosmosEventId.NoPartitionKeyDefined)));

    // EF Core 11 NorthwindQueryCosmosFixture, lines 34-100.
    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);

        modelBuilder.Entity<NorthwindCustomer>().ToContainer("Customers");
        modelBuilder.Entity<Employee>().ToContainer("Employees");

        modelBuilder.Entity<Order>()
            .HasRootDiscriminatorInJsonId()
            .ToContainer("ProductsAndOrders");

        modelBuilder.Entity<OrderDetail>()
            .HasRootDiscriminatorInJsonId()
            .ToContainer("ProductsAndOrders");

        modelBuilder.Entity<Product>()
            .HasRootDiscriminatorInJsonId()
            .ToContainer("ProductsAndOrders");

        var orderQuery = modelBuilder.Entity<OrderQuery>().ToContainer("ProductsAndOrders").HasRootDiscriminatorInJsonId();
        orderQuery.HasDiscriminator().HasValue("Order");
        orderQuery.Property<string>("Discriminator").ToJsonProperty("$type");

        var productQuery = modelBuilder
            .Entity<ProductQuery>()
            .ToContainer("ProductsAndOrders")
            .HasRootDiscriminatorInJsonId();
        productQuery.HasDiscriminator().HasValue("Product");
        productQuery.Property<string>("Discriminator").ToJsonProperty("$type");

        var productView = modelBuilder
            .Entity<ProductView>()
            .ToContainer("ProductsAndOrders")
            .HasRootDiscriminatorInJsonId();
        productView.HasDiscriminator().HasValue("ProductView");
        productView.Property<string>("Discriminator").ToJsonProperty("$type");

        var customerQueryWithQueryFilter = modelBuilder
            .Entity<CustomerQueryWithQueryFilter>()
            .ToContainer("Customers");
        customerQueryWithQueryFilter.HasDiscriminator().HasValue("Customer");
        customerQueryWithQueryFilter.Property<string>("Discriminator").ToJsonProperty("$type");

        var customerQuery = modelBuilder
            .Entity<CustomerQuery>()
            .ToContainer("Customers");
        customerQuery.HasDiscriminator().HasValue("Customer");
        customerQuery.Property<string>("Discriminator").ToJsonProperty("$type");

        modelBuilder.Entity<NorthwindCustomer>().Metadata.RemoveIndex(
            modelBuilder.Entity<NorthwindCustomer>().Property(e => e.City).Metadata.GetContainingIndexes().Single());

        modelBuilder.Entity<NorthwindCustomer>().Metadata.RemoveIndex(
            modelBuilder.Entity<NorthwindCustomer>().Property(e => e.CompanyName).Metadata.GetContainingIndexes().Single());

        modelBuilder.Entity<NorthwindCustomer>().Metadata.RemoveIndex(
            modelBuilder.Entity<NorthwindCustomer>().Property(e => e.PostalCode).Metadata.GetContainingIndexes().Single());

        modelBuilder.Entity<NorthwindCustomer>().Metadata.RemoveIndex(
            modelBuilder.Entity<NorthwindCustomer>().Property(e => e.Region).Metadata.GetContainingIndexes().Single());

        modelBuilder.Entity<Order>().Metadata.RemoveIndex(
            modelBuilder.Entity<Order>().Property(e => e.OrderDate).Metadata.GetContainingIndexes().Single());

        modelBuilder.Entity<Product>().Metadata.RemoveIndex(
            modelBuilder.Entity<Product>().Property(e => e.ProductName).Metadata.GetContainingIndexes().Single());
    }
}
