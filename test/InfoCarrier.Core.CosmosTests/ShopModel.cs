// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.CosmosTests;

public class Customer
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Country { get; set; } = null!;
    public Address Address { get; set; } = null!;
    public Address? BillingAddress { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

public class Address
{
    public string City { get; set; } = null!;
    public string Postcode { get; set; } = null!;
    public GeoLocation? Location { get; set; }
    public List<DeliveryWindow> Windows { get; set; } = [];
}

public class GeoLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class DeliveryWindow
{
    public string Day { get; set; } = "";
    public int Hour { get; set; }
}

public class Receipt
{
    public Guid Id { get; set; }
    public string Partition { get; set; } = "receipts";
    public string Description { get; set; } = "";
}

public class OrderLine
{
    public string Sku { get; set; } = null!;
    public int Quantity { get; set; }
}

public class Warehouse
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Version { get; set; }
    public string? ETag { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

public class ShopServerContext(DbContextOptions<ShopServerContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Configure(modelBuilder);
        modelBuilder.Entity<Customer>().ToContainer("customers").HasPartitionKey(c => c.Id);
        modelBuilder.Entity<Warehouse>().ToContainer("warehouses").HasPartitionKey(w => w.Id);
        modelBuilder.Entity<Warehouse>().Property(w => w.ETag).IsETagConcurrency();
        modelBuilder.Entity<Receipt>().ToContainer("receipts").HasPartitionKey(r => r.Partition);
    }

    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().OwnsOne(c => c.Address, owned =>
        {
            owned.OwnsOne(a => a.Location);
            owned.OwnsMany(a => a.Windows);
        });
        modelBuilder.Entity<Customer>().OwnsOne(c => c.BillingAddress, owned =>
        {
            owned.OwnsOne(a => a.Location);
            owned.OwnsMany(a => a.Windows);
        });
        modelBuilder.Entity<Customer>().OwnsMany(c => c.Lines);
        modelBuilder.Entity<Warehouse>().OwnsMany(w => w.Lines);
        modelBuilder.Entity<Warehouse>().Property(w => w.ETag).IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
        modelBuilder.Entity<Receipt>().Property(r => r.Id).ValueGeneratedOnAdd();
    }
}

public class ShopClientContext(DbContextOptions<ShopClientContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => ShopServerContext.Configure(modelBuilder);
}
