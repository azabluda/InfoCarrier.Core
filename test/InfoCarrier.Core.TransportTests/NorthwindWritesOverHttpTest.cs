using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class NorthwindWritesOverHttpTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task Several_edits_cross_as_one_save()
    {
        using NorthwindContext context = NorthwindOverHttpTest.CreateClientContext(factory);

        List<OrderDetail> lines = await context.OrderDetails
            .Where(d => d.OrderId == 4)
            .OrderBy(d => d.ProductId)
            .ToListAsync();

        Assert.Equal(2, lines.Count);

        lines[0].Quantity += 1;
        lines[1].Quantity += 2;

        int written = await context.SaveChangesAsync();

        Assert.Equal(2, written);

        using NorthwindContext verify = NorthwindOverHttpTest.CreateClientContext(factory);
        List<int> quantities = await verify.OrderDetails
            .Where(d => d.OrderId == 4)
            .OrderBy(d => d.ProductId)
            .Select(d => d.Quantity)
            .ToListAsync();

        Assert.Equal([8, 12], quantities);
    }

    [Fact]
    public async Task An_insert_gets_its_store_generated_key_back()
    {
        using NorthwindContext context = NorthwindOverHttpTest.CreateClientContext(factory);

        var category = new Category { Name = "Seafood" };
        context.Categories.Add(category);

        await context.SaveChangesAsync();

        // The client held a temporary placeholder before the save; the store's own key comes
        // back by correlation id (research-findings 9).
        Assert.True(category.Id > 0);
    }

    [Fact]
    public async Task A_rolled_back_transaction_leaves_the_store_untouched()
    {
        using NorthwindContext context = NorthwindOverHttpTest.CreateClientContext(factory);

        int before = await context.Products.CountAsync();

        using (IDbContextTransaction transaction = await context.Database.BeginTransactionAsync())
        {
            context.Products.Add(
                new Product { Name = "Rolled back", UnitPrice = 1.0m, UnitsInStock = 1, CategoryId = 1 });

            await context.SaveChangesAsync();

            await transaction.RollbackAsync();
        }

        using NorthwindContext verify = NorthwindOverHttpTest.CreateClientContext(factory);
        Assert.Equal(before, await verify.Products.CountAsync());
    }

    [Fact]
    public async Task A_committed_transaction_is_visible_to_a_later_context()
    {
        using NorthwindContext context = NorthwindOverHttpTest.CreateClientContext(factory);

        using (IDbContextTransaction transaction = await context.Database.BeginTransactionAsync())
        {
            context.Products.Add(
                new Product { Name = "Committed", UnitPrice = 2.0m, UnitsInStock = 4, CategoryId = 1 });

            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }

        using NorthwindContext verify = NorthwindOverHttpTest.CreateClientContext(factory);
        Assert.True(await verify.Products.AnyAsync(p => p.Name == "Committed"));
    }
}
