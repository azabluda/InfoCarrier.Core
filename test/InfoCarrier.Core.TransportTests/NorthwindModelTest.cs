// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Northwind.Shared;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class NorthwindModelTest
{
    [Fact]
    public void The_model_declares_the_five_entity_types_the_wire_will_name()
    {
        using var context = new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseSqlite("Filename=:memory:")
                .UseLazyLoadingProxies()
                .Options);

        string[] names = context.Model.GetEntityTypes()
            .Select(e => e.ClrType.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Category", "Customer", "Order", "OrderDetail", "Product"], names);
    }

    [Fact]
    public void An_order_detail_is_keyed_by_order_and_product()
    {
        using var context = new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseSqlite("Filename=:memory:")
                .UseLazyLoadingProxies()
                .Options);

        IKey key = context.Model.FindEntityType(typeof(Northwind.Shared.Model.OrderDetail))!.FindPrimaryKey()!;

        Assert.Equal(["OrderId", "ProductId"], key.Properties.Select(p => p.Name));
    }

    [Fact]
    public void The_seed_is_idempotent()
    {
        DbContextOptions<NorthwindContext> options = new DbContextOptionsBuilder<NorthwindContext>()
            .UseSqlite("Filename=:memory:")
            .Options;

        // One open connection keeps an in-memory SQLite database alive for the test's lifetime.
        using var context = new NorthwindContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        NorthwindSeed.Seed(context);
        int afterFirst = context.Customers.Count();

        NorthwindSeed.Seed(context);

        Assert.Equal(65, afterFirst);
        Assert.Equal(afterFirst, context.Customers.Count());
    }
}
