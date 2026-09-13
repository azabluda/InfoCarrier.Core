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

    /// <summary>
    ///     Emptying a nested array must persist as empty, and must not be repaired back (#102).
    /// </summary>
    /// <remarks>
    ///     <b>THIS IS THE ONE THE SERVER-SIDE COMPENSATION COULD GET EXACTLY WRONG.</b> That
    ///     compensation reads the stored document wherever the change set does not mention an owned
    ///     navigation the model declares, because a change set that omits one would otherwise erase
    ///     it. An array the application deliberately emptied looks like the same thing from the
    ///     server's side: nothing to see. If the two were confused, the old elements would come
    ///     back and the save would report success — data resurrection rather than data loss, and
    ///     the harder of the two to notice.
    /// </remarks>
    [Fact]
    public async Task Emptying_a_nested_array_persists_as_empty()
    {
        await Given("frank", [new OrderLine { Sku = "book", Quantity = 1 }, new OrderLine { Sku = "lamp", Quantity = 2 }]);

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer frank = await write.Customers.SingleAsync(c => c.Id == "frank");
            frank.Lines.Clear();
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "frank");

        Assert.Empty(reloaded.Lines);
        Assert.Equal("Cork", reloaded.Address.City);
    }

    /// <summary>
    ///     Removing one element of a nested array keeps the others.
    /// </summary>
    /// <remarks>
    ///     The half of the previous test that a whole-document rewrite can still get wrong in the
    ///     other direction: the removed element coming back, or the surviving ones going with it.
    /// </remarks>
    [Fact]
    public async Task Removing_one_element_of_a_nested_array_persists()
    {
        await Given("grace", [new OrderLine { Sku = "book", Quantity = 1 }, new OrderLine { Sku = "lamp", Quantity = 2 }]);

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer grace = await write.Customers.SingleAsync(c => c.Id == "grace");
            grace.Lines.RemoveAll(l => l.Sku == "book");
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "grace");

        OrderLine survivor = Assert.Single(reloaded.Lines);
        Assert.Equal("lamp", survivor.Sku);
        Assert.Equal(2, survivor.Quantity);
    }

    /// <summary>
    ///     Changing one element of a nested array in place leaves its neighbour alone (#107).
    /// </summary>
    /// <remarks>
    ///     <b>This threw rather than losing data, and what it threw on was the server returning a
    ///     KEY for a row that already existed.</b> MongoDB marks an owned collection's ordinal
    ///     <c>OnAddOrUpdate</c> and this client marks it <c>OnAdd</c>, so the server sent it back
    ///     for every modified element and applying it gave one element the key of its sibling.
    ///     <c>ServerSideControlTest.Server_side_nested_array_element_update_persists</c> is the
    ///     control that placed the fault on this side of the wire.
    /// </remarks>
    [Fact]
    public async Task Changing_one_element_of_a_nested_array_persists()
    {
        await Given("heidi", [new OrderLine { Sku = "book", Quantity = 1 }, new OrderLine { Sku = "lamp", Quantity = 2 }]);

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer heidi = await write.Customers.SingleAsync(c => c.Id == "heidi");
            heidi.Lines.Single(l => l.Sku == "book").Quantity = 9;
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "heidi");

        Assert.Equal(9, reloaded.Lines.Single(l => l.Sku == "book").Quantity);
        Assert.Equal(2, reloaded.Lines.Single(l => l.Sku == "lamp").Quantity);
    }

    /// <summary>
    ///     Setting an optional nested document to null must persist as absent (#102).
    /// </summary>
    /// <remarks>
    ///     The reference counterpart of the emptied array, and the same trap: an owned navigation
    ///     the application removed on purpose and one the change set merely failed to mention
    ///     arrive at the server looking alike. The required <see cref="Customer.Address" /> beside
    ///     it is asserted too, because a repair that overreached would restore both.
    /// </remarks>
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
            await seed.SaveChangesAsync();
        }

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer ivan = await write.Customers.SingleAsync(c => c.Id == "ivan");
            ivan.BillingAddress = null;
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "ivan");

        Assert.Null(reloaded.BillingAddress);
        Assert.Equal("Brno", reloaded.Address.City);
    }

    /// <summary>
    ///     A concurrency token on a document refuses a write made against a stale copy.
    /// </summary>
    /// <remarks>
    ///     <b>What this asks of the wire is that the ORIGINAL value crosses it.</b> A store that
    ///     rewrites the whole document still has to filter that write on the value the row held
    ///     when the client read it, and the client sends only what it holds. Tier B covers the
    ///     mechanism against a relational store; this asks it of a store where the unit of write is
    ///     the document.
    /// </remarks>
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
            await seed.SaveChangesAsync();
        }

        await using ShopClientContext first = fixture.CreateNonRelationalClient();
        await using ShopClientContext second = fixture.CreateNonRelationalClient();

        Warehouse mine = await first.Warehouses.SingleAsync(w => w.Id == "depot");
        Warehouse stale = await second.Warehouses.SingleAsync(w => w.Id == "depot");

        mine.Name = "Depot North";
        mine.Version++;
        await first.SaveChangesAsync();

        stale.Name = "Depot South";
        stale.Version++;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Warehouse reloaded = await read.Warehouses.SingleAsync(w => w.Id == "depot");

        Assert.Equal("Depot North", reloaded.Name);
        Assert.Equal("pallet", Assert.Single(reloaded.Lines).Sku);
    }

    /// <summary>
    ///     Seeds one customer with the given nested array, so no two tests share a document.
    /// </summary>
    /// <remarks>
    ///     xUnit does not order the tests in a class, and every test here mutates. A test that
    ///     writes its own root cannot be broken by a neighbour, which is cheaper to keep true than
    ///     an ordering rule nobody can see.
    /// </remarks>
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
        await seed.SaveChangesAsync();
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
