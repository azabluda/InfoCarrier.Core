// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class CosmosQueryComparisonTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
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
