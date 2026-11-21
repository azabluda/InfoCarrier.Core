// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class NonRelationalStoreTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    [Fact]
    public async Task The_client_works_without_being_told_the_store_is_not_relational()
    {
        // The default is the relational one, and a document store still answers ordinary queries.
        await using ShopClientContext db = fixture.CreateClient();

        Assert.Equal(3, await db.Customers.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_client_works_when_told_the_store_is_not_relational()
    {
        await using ShopClientContext db = fixture.CreateNonRelationalClient();

        List<Customer> all = await db.Customers.OrderBy(c => c.Id).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, all.Count);
        Assert.Equal("Berlin", all[0].Address.City);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Anonymous_collection_distinct_follows_backend_tracking_rules(bool nonRelational)
    {
        var options = new DbContextOptionsBuilder<ShopClientContext>(nonRelational
            ? fixture.NonRelationalClientOptions : fixture.ClientOptions);
        await using ShopClientContext document = new(options.Options);
        var query = document.Customers
            .Select(c => new { c.Name, c.Lines })
            .Distinct();
        InvalidOperationException refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Contains("owned entity without a corresponding owner", refusal.Message, StringComparison.Ordinal);
        using IServiceScope scope = fixture.ServerProvider.CreateScope();
        await using ShopServerContext backend = scope.ServiceProvider.GetRequiredService<ShopServerContext>();
        var direct = backend.Customers.Select(c => new { c.Name, c.Lines }).Distinct();
        InvalidOperationException expectedTracking = await Assert.ThrowsAsync<InvalidOperationException>(() => direct.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectedTracking.Message, refusal.Message);
        var expected = (await direct.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken)).OrderBy(c => c.Name).ToList();
        var actual = (await query.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken)).OrderBy(c => c.Name).ToList();
        Assert.Equal(3, actual.Count);
        Assert.Equal(expected.Select(c => c.Name), actual.Select(c => c.Name));
        Assert.Equal(expected.SelectMany(c => c.Lines).Select(l => (l.Sku, l.Quantity)),
            actual.SelectMany(c => c.Lines).Select(l => (l.Sku, l.Quantity)));
    }

    [Fact]
    public async Task Ordinary_use_never_builds_the_relational_model()
    {
        await using ShopClientContext db = fixture.CreateNonRelationalClient();

        Assert.Single(await db.Customers.Where(c => c.Address.City == "Berlin").ToListAsync(TestContext.Current.CancellationToken));

        Assert.NotNull(db.Model.FindRuntimeAnnotation("Relational:RelationalModelFactory"));
        Assert.Null(db.Model.FindRuntimeAnnotation("Relational:RelationalModel"));
    }
}
