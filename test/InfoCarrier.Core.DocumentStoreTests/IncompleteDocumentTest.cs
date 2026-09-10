// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     Change sets that do not mention the whole document, against a server that says it is a
///     document store (#102).
/// </summary>
/// <remarks>
///     <para>
///         <b>Every client here is one whose change set is incomplete</b>, and the two ways of
///         getting there are both represented because they are reached by different mistakes. A
///         client that was never told the store is not relational sends a bare root always. A
///         client that WAS told still sends a bare root when the application attached a stub
///         instead of reading the row, which is the ordinary relational way to update one field
///         and stays ordinary here.
///     </para>
///     <para>
///         <b>Without the server half, every one of these writes a document with the nested parts
///         gone</b>, reports success, and fails on the next read. That is #100 arriving from the
///         direction its own fix cannot cover: the client can only send what its change tracker
///         holds.
///     </para>
///     <para>
///         <b>There is no test here asserting the loss on an undeclared server</b>, and that is
///         deliberate. What a deployment which says nothing needs is that nothing changes for it,
///         and the evidence for that is the whole specification suite: 29,000 tests across three
///         relational tiers, none of which registers this. <see cref="UndeclaredDocumentStoreTest" />
///         covers the one thing that suite cannot, which is that the client half still works alone.
///     </para>
/// </remarks>
public class IncompleteDocumentTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    /// <summary>
    ///     The headline case: a deployment that forgot <c>UseNonRelationalServerStore()</c>.
    /// </summary>
    [Fact]
    public async Task A_scalar_update_from_a_client_that_was_never_told_keeps_the_nested_document()
    {
        await using (ShopClientContext write = fixture.CreateClient())
        {
            Customer bob = await write.Customers.SingleAsync(c => c.Id == "bob");
            bob.Name = "Robert";
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "bob");

        Assert.Equal("Robert", reloaded.Name);
        Assert.Equal("Lisbon", reloaded.Address.City);
        Assert.Equal(["book", "lamp"], reloaded.Lines.OrderBy(l => l.Sku).Select(l => l.Sku));
    }

    /// <summary>
    ///     A change to one nested part must not take the parts beside it with it.
    /// </summary>
    /// <remarks>
    ///     This one arrives at the server as an owned entry with no root at all, so the repair has
    ///     to work upwards: the root is derived from the owned entry's own key and read back too.
    /// </remarks>
    [Fact]
    public async Task A_nested_change_from_a_client_that_was_never_told_keeps_its_siblings()
    {
        await using (ShopClientContext write = fixture.CreateClient())
        {
            Customer alice = await write.Customers.SingleAsync(c => c.Id == "alice");
            alice.Address.City = "Munich";
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "alice");

        Assert.Equal("Munich", reloaded.Address.City);
        Assert.Equal("Alice", reloaded.Name);
        Assert.Equal("book", Assert.Single(reloaded.Lines).Sku);
    }

    /// <summary>
    ///     The hole the client half cannot close, on a client that is configured correctly.
    /// </summary>
    /// <remarks>
    ///     <b>Attaching a stub is how an application updates one field without reading the row</b>,
    ///     and it is right on a relational store. The change tracker then holds a customer and
    ///     nothing else, so <c>UseNonRelationalServerStore()</c> has nothing more to send, and the
    ///     document would be written with the nested parts it never knew about erased.
    /// </remarks>
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
            await seed.SaveChangesAsync();
        }

        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            var stub = new Customer { Id = "frank", Name = "François", Country = "FR" };
            write.Attach(stub);
            write.Entry(stub).Property(c => c.Name).IsModified = true;
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "frank");

        Assert.Equal("François", reloaded.Name);
        Assert.Equal("Lyon", reloaded.Address.City);
        Assert.Equal("clock", Assert.Single(reloaded.Lines).Sku);
    }

    /// <summary>
    ///     An insert has nothing to preserve, and is never read back.
    /// </summary>
    /// <remarks>
    ///     It works from a client that was never told, and always did: everything in a new document
    ///     is <c>Added</c>, so EF hands all of it to the client's <c>IDatabase</c> and all of it
    ///     travels. Here so that narrowing the repair away from inserts stays honest.
    /// </remarks>
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
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "gina");

        Assert.Equal("Bologna", reloaded.Address.City);
        Assert.Equal("map", Assert.Single(reloaded.Lines).Sku);
    }

    /// <summary>
    ///     A delete has nothing to keep, and is never read back either.
    /// </summary>
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
            await seed.SaveChangesAsync();
        }

        await using (ShopClientContext write = fixture.CreateClient())
        {
            Customer hugo = await write.Customers.SingleAsync(c => c.Id == "hugo");
            write.Customers.Remove(hugo);
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Assert.False(await read.Customers.AnyAsync(c => c.Id == "hugo"));
    }
}

/// <summary>
///     The client half of #100, against a server that was told nothing (#102).
/// </summary>
/// <remarks>
///     <b>A repair that also hid a regression in the thing it repairs would be worse than no
///     repair.</b> Every other write test in this tier now runs against a server that completes
///     incomplete change sets, so none of them can any longer tell whether
///     <c>UseNonRelationalServerStore()</c> is still sending the whole document. This one can: its
///     server does nothing, so a client that stopped expanding would fail here.
/// </remarks>
public class UndeclaredDocumentStoreTest(UndeclaredDocumentStoreFixture fixture)
    : IClassFixture<UndeclaredDocumentStoreFixture>
{
    [Fact]
    public async Task The_client_half_alone_writes_a_complete_document()
    {
        await using (ShopClientContext write = fixture.CreateNonRelationalClient())
        {
            Customer bob = await write.Customers.SingleAsync(c => c.Id == "bob");
            bob.Name = "Robert";
            await write.SaveChangesAsync();
        }

        await using ShopClientContext read = fixture.CreateNonRelationalClient();
        Customer reloaded = await read.Customers.SingleAsync(c => c.Id == "bob");

        Assert.Equal("Robert", reloaded.Name);
        Assert.Equal("Lisbon", reloaded.Address.City);
        Assert.Equal(2, reloaded.Lines.Count);
    }
}
