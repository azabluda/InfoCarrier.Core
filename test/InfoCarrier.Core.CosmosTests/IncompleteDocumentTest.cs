// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class IncompleteDocumentTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    [Fact]
    public async Task A_scalar_update_from_a_client_that_was_never_told_keeps_the_nested_document()
    {
        await using (ShopClientContext write = fixture.CreateClient())
        {
            Customer bob = await write.Customers.SingleAsync(c => c.Id == "bob", TestContext.Current.CancellationToken);
            bob.Name = "Robert";
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "bob", TestContext.Current.CancellationToken);

        Assert.Equal("Robert", reloaded.Name);
        Assert.Equal("Lisbon", reloaded.Address.City);
        Assert.Equal(["book", "lamp"], reloaded.Lines.OrderBy(l => l.Sku).Select(l => l.Sku));
    }

    [Fact]
    public async Task A_nested_change_from_a_client_that_was_never_told_keeps_its_siblings()
    {
        await using (ShopClientContext write = fixture.CreateClient())
        {
            Customer alice = await write.Customers.SingleAsync(c => c.Id == "alice", TestContext.Current.CancellationToken);
            alice.Address.City = "Munich";
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "alice", TestContext.Current.CancellationToken);

        Assert.Equal("Munich", reloaded.Address.City);
        Assert.Equal("Alice", reloaded.Name);
        Assert.Equal("book", Assert.Single(reloaded.Lines).Sku);
    }

    [Fact]
    public async Task An_attached_stub_keeps_the_nested_document()
    {
        await using (ShopClientContext seed = fixture.CreateNonRelationalClient())
        {
            seed.Customers.Add(new Customer
            {
                Id = "frank",
                Name = "Frank",
                Country = "FR",
                Address = new Address { City = "Lyon", Postcode = "69001" },
                Lines = [new OrderLine { Sku = "clock", Quantity = 2 }],
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            var stub = new Customer { Id = "frank", Name = "François", Country = "FR" };
            write.Attach(stub);
            write.Entry(stub).Property(c => c.Name).IsModified = true;
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "frank", TestContext.Current.CancellationToken);

        Assert.Equal("François", reloaded.Name);
        Assert.Equal("Lyon", reloaded.Address.City);
        Assert.Equal("clock", Assert.Single(reloaded.Lines).Sku);
    }

    [Fact]
    public async Task An_insert_from_a_client_that_was_never_told_still_carries_its_nested_shapes()
    {
        await using (ShopClientContext write = fixture.CreateClient())
        {
            write.Customers.Add(new Customer
            {
                Id = "gina",
                Name = "Gina",
                Country = "IT",
                Address = new Address { City = "Bologna", Postcode = "40121" },
                Lines = [new OrderLine { Sku = "map", Quantity = 5 }],
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "gina", TestContext.Current.CancellationToken);

        Assert.Equal("Bologna", reloaded.Address.City);
        Assert.Equal("map", Assert.Single(reloaded.Lines).Sku);
    }

    [Fact]
    public async Task A_delete_from_a_client_that_was_never_told_removes_the_document()
    {
        await using (ShopClientContext seed = fixture.CreateNonRelationalClient())
        {
            seed.Customers.Add(new Customer
            {
                Id = "hugo",
                Name = "Hugo",
                Country = "BE",
                Address = new Address { City = "Ghent", Postcode = "9000" },
            });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (ShopClientContext write = fixture.CreateClient())
        {
            Customer hugo = await write.Customers.SingleAsync(c => c.Id == "hugo", TestContext.Current.CancellationToken);
            write.Customers.Remove(hugo);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Assert.False(await read.Customers.AnyAsync(c => c.Id == "hugo", TestContext.Current.CancellationToken));
    }
}

public class UndeclaredDocumentStoreTest(UndeclaredDocumentStoreFixture fixture)
    : IClassFixture<UndeclaredDocumentStoreFixture>
{
    [Fact]
    public async Task The_client_half_alone_writes_a_complete_document()
    {
        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer bob = await write.Customers.SingleAsync(c => c.Id == "bob", TestContext.Current.CancellationToken);
            bob.Name = "Robert";
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using ShopServerContext read = fixture.CreateBackend();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "bob", TestContext.Current.CancellationToken);

        Assert.Equal("Robert", reloaded.Name);
        Assert.Equal("Lisbon", reloaded.Address.City);
        Assert.Equal(2, reloaded.Lines.Count);
    }
}
