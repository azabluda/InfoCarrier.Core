// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class CosmosQueryComparisonTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    [Theory]
    [InlineData("alice", "Alice")]
    [InlineData("bob", "Bob")]
    public async Task Point_reads_use_the_requested_key_among_multiple_documents(string id, string name)
    {
        var owner = Assert.IsType<CurrentTest>(CurrentTest.Value);
        var reads = new List<(string Id, string Container, string Partition)>();
        using var observer = new CosmosQueryCapture(item =>
        {
            if (ReferenceEquals(CurrentTest.Value, owner))
                reads.Add((item.ResourceId, item.ContainerId, item.PartitionKeyValue.ToString()));
        });
        using var direct = fixture.CreateBackend();
        Assert.Equal(3, await direct.Customers.CountAsync(TestContext.Current.CancellationToken));
        var expected = await direct.Customers.AsNoTracking().SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
        AssertRead(expected);
        reads.Clear();
        using var client = fixture.CreateNonRelationalClient();
        var actual = await client.Customers.AsNoTracking().SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
        AssertRead(actual);

        void AssertRead(Customer customer)
        {
            Assert.Equal(id, customer.Id);
            Assert.Equal(name, customer.Name);
            var read = Assert.Single(reads);
            Assert.Equal(id, read.Id);
            Assert.Equal("customers", read.Container);
            Assert.Equal(new Microsoft.Azure.Cosmos.PartitionKey(id).ToString(), read.Partition);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Filter_projection_order_and_limit_match_the_direct_store(bool nested)
    {
        using var capture = LiveComparison.IsEnabled ? null : new CosmosQueryCapture();
        var test = Assert.IsType<CurrentTest>(CurrentTest.Value);
        int start = test.Commands.Count;
        using var direct = fixture.CreateBackend();
        string country = "DE";
        IQueryable<string> Query(DbContext context) => nested
            ? context.Set<Customer>().Where(c => c.Country == country).OrderBy(c => c.Id).Take(2).Select(c => c.Address!.City)
            : context.Set<Customer>().Where(c => c.Country == country).OrderBy(c => c.Id).Take(2).Select(c => c.Name);
        var expected = await Query(direct).ToArrayAsync(TestContext.Current.CancellationToken);
        var plain = test.Commands.Skip(start).ToArray();
        Assert.NotEmpty(plain);
        start = test.Commands.Count;
        using var client = fixture.CreateNonRelationalClient();
        var actual = await Query(client).ToArrayAsync(TestContext.Current.CancellationToken);
        var wire = test.Commands.Skip(start).ToArray();
        Assert.NotEmpty(wire);
        Assert.Equal(expected, actual);
        Assert.Equal(plain.Select(c => c.Text), wire.Select(c => c.Text));
    }

    [Fact]
    public void Canonicalization_preserves_literals_json_names_and_parameter_reuse()
    {
        const string first = "SELECT VALUE {\"@name\": c[\"Name\"], \"value\": \"@city  literal\"} FROM c WHERE c[\"City\"] = @city OR c[\"Name\"] = @city";
        const string renamed = "SELECT VALUE {\"@name\": c[\"Name\"], \"value\": \"@city  literal\"} FROM c WHERE c[\"City\"] = @Value OR c[\"Name\"] = @Value";
        Assert.Equal(CosmosQueryCapture.CanonicalQuery(first), CosmosQueryCapture.CanonicalQuery(renamed));
        Assert.Contains("\"@city  literal\"", CosmosQueryCapture.CanonicalQuery(first));
        Assert.NotEqual(CosmosQueryCapture.CanonicalQuery(first),
            CosmosQueryCapture.CanonicalQuery(first.Replace("= @city OR", "= \"DE\" OR", StringComparison.Ordinal)));
        Assert.NotEqual(CosmosQueryCapture.CanonicalQuery(first),
            CosmosQueryCapture.CanonicalQuery(first.Replace("OR c[\"Name\"] = @city", "OR c[\"Name\"] = @other", StringComparison.Ordinal)));
    }
}
