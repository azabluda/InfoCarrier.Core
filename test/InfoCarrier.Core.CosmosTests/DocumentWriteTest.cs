// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class DocumentWriteTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    [Fact]
    public async Task An_insert_carrying_nested_shapes_round_trips()
    {
        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            write.Customers.Add(new Customer
            {
                Id = "dave",
                Name = "Dave",
                Country = "NL",
                Address = new Address { City = "Utrecht", Postcode = "3511" },
                Lines = [new OrderLine { Sku = "desk", Quantity = 1 }],
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer dave = await read.Customers.SingleAsync(c => c.Id == "dave", TestContext.Current.CancellationToken);

        Assert.Equal("Utrecht", dave.Address.City);
        Assert.Equal("desk", Assert.Single(dave.Lines).Sku);
    }

    [Fact]
    public async Task Updating_a_nested_document_persists()
    {
        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer alice = await write.Customers.SingleAsync(c => c.Id == "alice", TestContext.Current.CancellationToken);
            alice.Address.City = "Munich";
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Assert.Equal("Munich", (await read.Customers.SingleAsync(c => c.Id == "alice", TestContext.Current.CancellationToken)).Address.City);
    }

    [Fact]
    public async Task Adding_to_a_nested_array_persists()
    {
        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer carol = await write.Customers.SingleAsync(c => c.Id == "carol", TestContext.Current.CancellationToken);
            carol.Lines.Add(new OrderLine { Sku = "chair", Quantity = 4 });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "carol", TestContext.Current.CancellationToken);

        Assert.Equal("chair", Assert.Single(reloaded.Lines).Sku);
    }

    [Fact]
    public async Task A_scalar_update_keeps_the_nested_document()
    {
        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer bob = await write.Customers.SingleAsync(c => c.Id == "bob", TestContext.Current.CancellationToken);
            bob.Name = "Robert";
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer bob2 = await read.Customers.SingleAsync(c => c.Id == "bob", TestContext.Current.CancellationToken);

        Assert.Equal("Robert", bob2.Name);
        Assert.Equal("Lisbon", bob2.Address.City);
    }

    [Fact]
    public async Task Emptying_a_nested_array_persists_as_empty()
    {
        await Given("frank", [new OrderLine { Sku = "book", Quantity = 1 }, new OrderLine { Sku = "lamp", Quantity = 2 }]);

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer frank = await write.Customers.SingleAsync(c => c.Id == "frank", TestContext.Current.CancellationToken);
            frank.Lines.Clear();
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "frank", TestContext.Current.CancellationToken);

        Assert.Empty(reloaded.Lines);
        Assert.Equal("Cork", reloaded.Address.City);
    }

    [Fact]
    public async Task Removing_one_element_of_a_nested_array_persists()
    {
        await Given("grace", [new OrderLine { Sku = "book", Quantity = 1 }, new OrderLine { Sku = "lamp", Quantity = 2 }]);

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer grace = await write.Customers.SingleAsync(c => c.Id == "grace", TestContext.Current.CancellationToken);
            grace.Lines.RemoveAll(l => l.Sku == "book");
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "grace", TestContext.Current.CancellationToken);

        OrderLine survivor = Assert.Single(reloaded.Lines);
        Assert.Equal("lamp", survivor.Sku);
        Assert.Equal(2, survivor.Quantity);
    }

    [Fact]
    public async Task Changing_one_element_of_a_nested_array_persists()
    {
        await Given("heidi", [new OrderLine { Sku = "book", Quantity = 1 }, new OrderLine { Sku = "lamp", Quantity = 2 }]);

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer heidi = await write.Customers.SingleAsync(c => c.Id == "heidi", TestContext.Current.CancellationToken);
            heidi.Lines.Single(l => l.Sku == "book").Quantity = 9;
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "heidi", TestContext.Current.CancellationToken);

        Assert.Equal(9, reloaded.Lines.Single(l => l.Sku == "book").Quantity);
        Assert.Equal(2, reloaded.Lines.Single(l => l.Sku == "lamp").Quantity);
    }

    [Fact]
    public async Task Clearing_an_optional_nested_document_persists()
    {
        await using (ShopClientContext seed = fixture.CreateNonRelationalClient())
        {
            seed.Customers.Add(new Customer
            {
                Id = "ivan",
                Name = "Ivan",
                Country = "CZ",
                Address = new Address { City = "Brno", Postcode = "60200" },
                BillingAddress = new Address { City = "Prague", Postcode = "11000" },
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer ivan = await write.Customers.SingleAsync(c => c.Id == "ivan", TestContext.Current.CancellationToken);
            ivan.BillingAddress = null;
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "ivan", TestContext.Current.CancellationToken);

        Assert.Null(reloaded.BillingAddress);
        Assert.Equal("Brno", reloaded.Address.City);
    }

    [Fact]
    public async Task A_stale_write_against_a_concurrency_token_is_refused()
    {
        await using (ShopClientContext seed = fixture.CreateNonRelationalClient())
        {
            seed.Warehouses.Add(new Warehouse
            {
                Id = "depot",
                Name = "Depot",
                Version = 1,
                Lines = [new OrderLine { Sku = "pallet", Quantity = 10 }],
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopClientContext first = fixture.CreateNonRelationalClient();
        await using ShopClientContext second = fixture.CreateNonRelationalClient();

        Warehouse mine = await first.Warehouses.SingleAsync(w => w.Id == "depot", TestContext.Current.CancellationToken);
        Warehouse stale = await second.Warehouses.SingleAsync(w => w.Id == "depot", TestContext.Current.CancellationToken);

        mine.Name = "Depot North";
        mine.Version++;
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);

        stale.Name = "Depot South";
        stale.Version++;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using ShopServerContext read = fixture.CreateBackend();
        Warehouse reloaded = await read.Warehouses.SingleAsync(w => w.Id == "depot", TestContext.Current.CancellationToken);

        Assert.Equal("Depot North", reloaded.Name);
        Assert.Equal("pallet", Assert.Single(reloaded.Lines).Sku);
    }

    private async Task Given(string id, List<OrderLine> lines)
    {
        await using ShopClientContext seed = fixture.CreateNonRelationalClient();
        seed.Customers.Add(new Customer
        {
            Id = id,
            Name = id,
            Country = "IE",
            Address = new Address { City = "Cork", Postcode = "T12" },
            Lines = lines,
        });
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_delete_removes_the_document()
    {
        await using (ShopClientContext seed = fixture.CreateNonRelationalClient())
        {
            seed.Customers.Add(new Customer
            {
                Id = "erin",
                Name = "Erin",
                Country = "IE",
                Address = new Address { City = "Cork", Postcode = "T12" },
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer erin = await write.Customers.SingleAsync(c => c.Id == "erin", TestContext.Current.CancellationToken);
            write.Customers.Remove(erin);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Assert.False(await read.Customers.AnyAsync(c => c.Id == "erin", TestContext.Current.CancellationToken));
    }
}
