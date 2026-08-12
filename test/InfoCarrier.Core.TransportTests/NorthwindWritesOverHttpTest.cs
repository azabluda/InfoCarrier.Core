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
        using NorthwindContext context = NorthwindOverHttpTest.CreateClientContext(factory, out RecordingHandler recorder);

        List<OrderDetail> lines = await context.OrderDetails
            .Where(d => d.OrderId == 4)
            .OrderBy(d => d.ProductId)
            .ToListAsync();

        Assert.Equal(2, lines.Count);

        lines[0].Quantity += 1;
        lines[1].Quantity += 2;

        // A SaveChangesAsync that shipped the two changed OrderDetail rows as two separate HTTP
        // round trips (one per entity) would still report written == 2 and leave the same end
        // state below -- only counting requests around the call itself can tell "one save" from
        // "two saves that happen to add up". Counted as a delta, not an absolute total, because
        // the preceding query above also costs a request.
        int requestCountBeforeSave = recorder.RequestCount;

        int written = await context.SaveChangesAsync();

        Assert.Equal(1, recorder.RequestCount - requestCountBeforeSave);
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

        // category.Id > 0 alone cannot tell "correlated to the right entity" from "correlated to
        // the wrong one" or "some unrelated positive integer came back" (a row count, a stale id
        // from a broken correlation-id lookup) -- both would also be > 0. Re-reading the row by
        // that id through a fresh context, over a fresh HTTP round trip, and checking it is the
        // *same* row (its Name is what this test wrote) excludes a mis-correlated key: a wrong id
        // would either fetch nothing (FirstOrDefaultAsync returns null) or fetch a different row
        // whose Name is not "Seafood".
        using NorthwindContext verify = NorthwindOverHttpTest.CreateClientContext(factory);
        Category? roundTripped = await verify.Categories.SingleOrDefaultAsync(c => c.Id == category.Id);

        Assert.NotNull(roundTripped);
        Assert.Equal("Seafood", roundTripped.Name);
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

            // Nothing so far proves the insert ever reached the store -- a SaveChangesAsync that
            // was a complete no-op inside an open transaction would leave the final count equal
            // to `before` and this test would pass exactly as it does when rollback genuinely
            // undoes a real write. Querying on the same context, inside the still-open
            // transaction, is what tells "rolled back a real write" apart from "never wrote
            // anything".
            Assert.Equal(before + 1, await context.Products.CountAsync());

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
