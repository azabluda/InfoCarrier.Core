// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     Writes that cross the wire and land in a document store.
/// </summary>
/// <remarks>
///     <para>
///         <b>A separate class so it gets a separate fixture, and therefore a separate server.</b>
///         That is deliberate: these tests mutate the seed, and the query class must not see the
///         mutation. It is also what makes this tier's parallelism honest, because xUnit runs the
///         classes at the same time against independent servers.
///     </para>
///     <para>
///         <b>EVERY CLIENT HERE IS TOLD THE STORE IS NOT RELATIONAL, and that is required
///         configuration rather than a convenience (#100).</b> A change to any part of a document
///         can only be written by writing the whole document, so the client has to send the whole
///         document, and it only knows to do that when the deployment has said what the store is.
///         `UseNonRelationalServerStore()` already carries exactly that statement and already
///         governs which queries the client will compose; it now governs how much of a document
///         travels with a change.
///     </para>
///     <para>
///         <b>A client that does NOT say it, against a document store, is misconfigured</b> in the
///         same way it already was for queries. `NonRelationalStoreTest` keeps one deliberately
///         misconfigured client so the difference between the two is visible.
///     </para>
/// </remarks>
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
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer dave = await read.Customers.SingleAsync(c => c.Id == "dave");

        Assert.Equal("Utrecht", dave.Address.City);
        Assert.Equal("desk", Assert.Single(dave.Lines).Sku);
    }

    [Fact]
    public async Task Updating_a_nested_document_persists()
    {
        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer alice = await write.Customers.SingleAsync(c => c.Id == "alice");
            alice.Address.City = "Munich";
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Assert.Equal("Munich", (await read.Customers.SingleAsync(c => c.Id == "alice")).Address.City);
    }

    [Fact]
    public async Task Adding_to_a_nested_array_persists()
    {
        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer carol = await write.Customers.SingleAsync(c => c.Id == "carol");
            carol.Lines.Add(new OrderLine { Sku = "chair", Quantity = 4 });
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "carol");

        Assert.Equal("chair", Assert.Single(reloaded.Lines).Sku);
    }

    /// <summary>
    ///     Updating only a scalar must not disturb the nested documents beside it (#100).
    /// </summary>
    /// <remarks>
    ///     <b>This one is data loss rather than a refusal when it fails</b>, which is why it
    ///     asserts the read as well as the write: the save reports success and the stored document
    ///     comes back missing <c>Address</c>.
    /// </remarks>
    [Fact]
    public async Task A_scalar_update_keeps_the_nested_document()
    {
        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer bob = await write.Customers.SingleAsync(c => c.Id == "bob");
            bob.Name = "Robert";
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer bob2 = await read.Customers.SingleAsync(c => c.Id == "bob");

        Assert.Equal("Robert", bob2.Name);
        Assert.Equal("Lisbon", bob2.Address.City);
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
            await seed.SaveChangesAsync();
        }

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer erin = await write.Customers.SingleAsync(c => c.Id == "erin");
            write.Customers.Remove(erin);
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Assert.False(await read.Customers.AnyAsync(c => c.Id == "erin"));
    }
}
