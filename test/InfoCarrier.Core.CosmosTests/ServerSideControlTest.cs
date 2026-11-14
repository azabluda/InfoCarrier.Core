// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class ServerSideControlTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    private ShopServerContext Server(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<ShopServerContext>();

    [Fact]
    public async Task Server_side_scalar_update_keeps_the_nested_document()
    {
        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer bob = await db.Customers.SingleAsync(c => c.Id == "bob", TestContext.Current.CancellationToken);
            bob.Name = "Robert";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer bob = await db.Customers.SingleAsync(c => c.Id == "bob", TestContext.Current.CancellationToken);

            Assert.Equal("Robert", bob.Name);
            Assert.Equal("Lisbon", bob.Address.City);
        }
    }

    [Fact]
    public async Task Server_side_nested_document_update_persists()
    {
        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer alice = await db.Customers.SingleAsync(c => c.Id == "alice", TestContext.Current.CancellationToken);
            alice.Address.City = "Munich";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Assert.Equal("Munich", (await db.Customers.SingleAsync(c => c.Id == "alice", TestContext.Current.CancellationToken)).Address.City);
        }
    }

    [Fact]
    public async Task Server_side_nested_array_element_update_persists()
    {
        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            db.Customers.Add(new Customer
            {
                Id = "judy",
                Name = "Judy",
                Country = "IE",
                Address = new Address { City = "Cork", Postcode = "T12" },
                Lines = [new OrderLine { Sku = "book", Quantity = 1 }, new OrderLine { Sku = "lamp", Quantity = 2 }],
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer judy = await db.Customers.SingleAsync(c => c.Id == "judy", TestContext.Current.CancellationToken);
            judy.Lines.Single(l => l.Sku == "book").Quantity = 9;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer judy = await db.Customers.SingleAsync(c => c.Id == "judy", TestContext.Current.CancellationToken);

            Assert.Equal(9, judy.Lines.Single(l => l.Sku == "book").Quantity);
            Assert.Equal(2, judy.Lines.Single(l => l.Sku == "lamp").Quantity);
        }
    }

    [Fact]
    public async Task Server_side_nested_array_append_persists()
    {
        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer carol = await db.Customers.SingleAsync(c => c.Id == "carol", TestContext.Current.CancellationToken);
            carol.Lines.Add(new OrderLine { Sku = "chair", Quantity = 4 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer carol = await db.Customers.SingleAsync(c => c.Id == "carol", TestContext.Current.CancellationToken);

            Assert.Equal("chair", Assert.Single(carol.Lines).Sku);
        }
    }
}
