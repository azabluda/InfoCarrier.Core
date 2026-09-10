// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     Queries over a shape that has no tables: a document inside a document, and an array inside
///     a document.
/// </summary>
/// <remarks>
///     <b>This is the half of Tier D that no other tier can express.</b> Against SQLite or Firebird
///     the same model becomes tables and joins, so a green test there proves the client can compose
///     over a relational store. Here there is no join to be got right or wrong, and a green test
///     proves the client did not need one.
/// </remarks>
public class NestedDocumentTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    [Fact]
    public async Task Every_seeded_customer_round_trips()
    {
        await using ShopClientContext db = fixture.CreateClient();

        List<Customer> all = await db.Customers.OrderBy(c => c.Id).ToListAsync();

        Assert.Equal(3, all.Count);
        Assert.Equal(["alice", "bob", "carol"], all.Select(c => c.Id));
    }

    [Fact]
    public async Task A_nested_document_comes_back_populated()
    {
        await using ShopClientContext db = fixture.CreateClient();

        Customer alice = await db.Customers.SingleAsync(c => c.Id == "alice");

        Assert.Equal("Berlin", alice.Address.City);
        Assert.Equal("10115", alice.Address.Postcode);
    }

    [Fact]
    public async Task A_filter_reaches_into_a_nested_document()
    {
        await using ShopClientContext db = fixture.CreateClient();

        List<Customer> berlin = await db.Customers
            .Where(c => c.Address.City == "Berlin")
            .ToListAsync();

        Assert.Equal("alice", Assert.Single(berlin).Id);
    }

    [Fact]
    public async Task A_projection_reaches_into_a_nested_document()
    {
        await using ShopClientContext db = fixture.CreateClient();

        var rows = await db.Customers
            .OrderBy(c => c.Id)
            .Select(c => new { c.Name, c.Address.City })
            .ToListAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal("Berlin", rows[0].City);
        Assert.Equal("Lisbon", rows[1].City);
    }

    [Fact]
    public async Task Ordering_by_a_nested_property_works()
    {
        await using ShopClientContext db = fixture.CreateClient();

        List<string> cities = await db.Customers
            .OrderBy(c => c.Address.City)
            .Select(c => c.Address.City)
            .ToListAsync();

        Assert.Equal(["Berlin", "Hamburg", "Lisbon"], cities);
    }

    [Fact]
    public async Task A_nested_array_comes_back_with_its_elements()
    {
        await using ShopClientContext db = fixture.CreateClient();

        Customer bob = await db.Customers.SingleAsync(c => c.Id == "bob");

        Assert.Equal(2, bob.Lines.Count);
        Assert.Contains(bob.Lines, l => l.Sku == "lamp" && l.Quantity == 3);
    }

    [Fact]
    public async Task An_empty_nested_array_comes_back_empty_rather_than_null()
    {
        await using ShopClientContext db = fixture.CreateClient();

        Customer carol = await db.Customers.SingleAsync(c => c.Id == "carol");

        Assert.NotNull(carol.Lines);
        Assert.Empty(carol.Lines);
    }

    [Fact]
    public async Task A_scalar_aggregate_is_answered_by_the_store()
    {
        await using ShopClientContext db = fixture.CreateClient();

        Assert.Equal(2, await db.Customers.CountAsync(c => c.Country == "DE"));
    }

    [Fact]
    public async Task Paging_over_documents_works()
    {
        await using ShopClientContext db = fixture.CreateClient();

        List<string> page = await db.Customers
            .OrderBy(c => c.Id)
            .Skip(1)
            .Take(1)
            .Select(c => c.Id)
            .ToListAsync();

        Assert.Equal("bob", Assert.Single(page));
    }

    [Fact]
    public async Task Find_by_a_string_key_works()
    {
        await using ShopClientContext db = fixture.CreateClient();

        Customer? found = await db.Customers.FindAsync("carol");

        Assert.NotNull(found);
        Assert.Equal("Hamburg", found.Address.City);
    }
}
