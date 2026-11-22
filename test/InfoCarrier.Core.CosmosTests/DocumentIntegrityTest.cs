// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class DocumentIntegrityTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    private DbContext Context(bool direct) => direct ? fixture.CreateBackend() : fixture.CreateNonRelationalClient();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nested_replacements_updates_and_deletions_persist(bool direct)
    {
        string id = Guid.NewGuid().ToString("N");
        await using (DbContext write = Context(direct))
        {
            write.Add(new Customer { Id = id, Name = "Deep", Country = "DE", Address = DeepAddress("First") });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await Verify("First", 51, ["Monday", "Tuesday"]);
        using (Microsoft.Azure.Cosmos.CosmosClient client = CosmosEmulator.Client())
        using (Microsoft.Azure.Cosmos.ResponseMessage response = await client.GetContainer(fixture.DatabaseName, "customers")
            .ReadItemStreamAsync(id, new Microsoft.Azure.Cosmos.PartitionKey(id), cancellationToken: TestContext.Current.CancellationToken))
        {
            response.EnsureSuccessStatusCode();
            using var document = await System.Text.Json.JsonDocument.ParseAsync(response.Content, cancellationToken: TestContext.Current.CancellationToken);
            System.Text.Json.JsonElement address = document.RootElement.GetProperty("Address");
            Assert.Equal(51, address.GetProperty("Location").GetProperty("Latitude").GetDouble());
            Assert.Equal(2, address.GetProperty("Windows").GetArrayLength());
        }
        await using (DbContext write = Context(direct))
        {
            Customer customer = await write.Set<Customer>().SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
            customer.Address.Location!.Latitude = 52;
            customer.Address.Windows[0].Hour = 11;
            customer.Address.Windows.RemoveAt(1);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await Verify("First", 52, ["Monday"]);
        await using (DbContext write = Context(direct))
        {
            Customer customer = await write.Set<Customer>().SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
            customer.Address = DeepAddress("Replacement");
            customer.BillingAddress = DeepAddress("Billing");
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await Verify("Replacement", 51, ["Monday", "Tuesday"]);
        await using (DbContext write = Context(direct))
        {
            Customer customer = await write.Set<Customer>().SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
            customer.Address.Windows.Clear();
            customer.Address.Location = null;
            customer.BillingAddress = null;
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using (ShopServerContext read = fixture.CreateBackend())
        {
            Customer stored = await read.Customers.SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
            Assert.Null(stored.Address.Location);
            Assert.Empty(stored.Address.Windows);
            Assert.Null(stored.BillingAddress);
        }
        await using (DbContext write = Context(direct))
        {
            Customer customer = await write.Set<Customer>().SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
            write.Remove(customer);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using ShopServerContext finalRead = fixture.CreateBackend();
        Assert.False(await finalRead.Customers.AnyAsync(c => c.Id == id, TestContext.Current.CancellationToken));

        async Task Verify(string city, double latitude, string[] days)
        {
            await using ShopServerContext read = fixture.CreateBackend();
            Customer stored = await read.Customers.SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
            Assert.Equal(city, stored.Address.City);
            Assert.Equal(latitude, stored.Address.Location!.Latitude);
            Assert.Equal(days, stored.Address.Windows.Select(w => w.Day));
            Assert.Equal(7, stored.Address.Location.Longitude);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scalar_updates_preserve_independently_changed_deep_documents(bool stub)
    {
        string id = Guid.NewGuid().ToString("N");
        await using (ShopServerContext seed = fixture.CreateBackend())
        {
            seed.Add(new Customer { Id = id, Name = "Before", Country = "DE", Address = DeepAddress("Before") });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using ShopClientContext write = fixture.CreateClient();
        Customer customer = stub ? new Customer { Id = id } : await write.Customers.SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
        if (stub) write.Attach(customer);
        await using (ShopServerContext other = fixture.CreateBackend())
        {
            Customer stored = await other.Customers.SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
            stored.Address.Location!.Latitude = 53;
            stored.Address.Windows.Add(new() { Day = "Friday", Hour = 17 });
            await other.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        customer.Name = "After";
        write.Entry(customer).Property(c => c.Name).IsModified = true;
        await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using ShopServerContext read = fixture.CreateBackend();
        Customer actual = await read.Customers.SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal("After", actual.Name);
        Assert.Equal("DE", actual.Country);
        Assert.Equal(53, actual.Address.Location!.Latitude);
        Assert.Equal(["Monday", "Tuesday", "Friday"], actual.Address.Windows.Select(w => w.Day));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generated_guid_keys_round_trip(bool direct)
    {
        var receipt = new Receipt { Description = "Generated" };
        await using (DbContext write = Context(direct))
        {
            write.Add(receipt);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.NotEqual(Guid.Empty, receipt.Id);
            Assert.Equal(EntityState.Unchanged, write.Entry(receipt).State);
        }
        await using ShopServerContext read = fixture.CreateBackend();
        Receipt actual = await read.Set<Receipt>().SingleAsync(r => r.Id == receipt.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Generated", actual.Description);
    }

    [Fact]
    public async Task Repair_does_not_replace_a_stale_original_concurrency_value()
    {
        string id = Guid.NewGuid().ToString("N");
        await using (ShopServerContext seed = fixture.CreateBackend())
        {
            seed.Warehouses.Add(new Warehouse { Id = id, Name = "Before", Lines = [new() { Sku = "keep", Quantity = 3 }] });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using ShopClientContext staleReader = fixture.CreateNonRelationalClient();
        Warehouse stale = await staleReader.Warehouses.SingleAsync(w => w.Id == id, TestContext.Current.CancellationToken);
        await using (ShopServerContext other = fixture.CreateBackend())
        {
            Warehouse current = await other.Warehouses.SingleAsync(w => w.Id == id, TestContext.Current.CancellationToken);
            current.Name = "Concurrent";
            current.Lines[0].Quantity = 4;
            await other.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using ShopClientContext writer = fixture.CreateNonRelationalClient();
        var stub = new Warehouse { Id = id, Name = "Stale", ETag = stale.ETag };
        writer.Attach(stub);
        writer.Entry(stub).Property(w => w.Name).IsModified = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => writer.SaveChangesAsync(TestContext.Current.CancellationToken));
        await using ShopServerContext read = fixture.CreateBackend();
        Warehouse actual = await read.Warehouses.SingleAsync(w => w.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal("Concurrent", actual.Name);
        Assert.Equal(4, Assert.Single(actual.Lines).Quantity);
    }

    private static Address DeepAddress(string city) => new()
    {
        City = city, Postcode = "12345", Location = new() { Latitude = 51, Longitude = 7 },
        Windows = [new() { Day = "Monday", Hour = 9 }, new() { Day = "Tuesday", Hour = 10 }],
    };
}
