using Microsoft.EntityFrameworkCore;
using Northwind.Shared.Model;

namespace Northwind.Shared;

/// <summary>
///     The one context type both halves use.
/// </summary>
/// <remarks>
///     Not a convenience. The wire carries entity type NAMES, so the client's model and the
///     server's must agree about them; one shared OnModelCreating makes that true by
///     construction rather than by discipline. See A49 in CLAUDE.md.
/// </remarks>
public class NorthwindContext(DbContextOptions<NorthwindContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().Property(e => e.Id).HasMaxLength(5).ValueGeneratedNever();

        modelBuilder.Entity<OrderDetail>().HasKey(e => new { e.OrderId, e.ProductId });

        modelBuilder.Entity<Order>()
            .HasMany(e => e.OrderDetails)
            .WithOne()
            .HasForeignKey(e => e.OrderId);

        modelBuilder.Entity<Order>()
            .HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId);

        modelBuilder.Entity<OrderDetail>()
            .HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId);
    }
}
