// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Query;

[WireFreeControl]
public class NorthwindProjectionControlTest(NorthwindFixture<NoopModelCustomizer> fixture)
    : IClassFixture<NorthwindFixture<NoopModelCustomizer>>
{
    // Official EF Core 11 miscellaneous suite, lines 4980-5009.
    [Fact]
    public async Task Paging_preserves_entity_results_in_the_direct_store()
    {
        using var context = ((InfoCarrierTestStore)fixture.TestStore).Backend.CreateDbContext();
        var customers = context.Set<Microsoft.EntityFrameworkCore.TestModels.Northwind.Customer>();
        int count = await customers.CountAsync(TestContext.Current.CancellationToken);
#pragma warning disable EF9102 // Direct control for official experimental paging tests.
        var all = await customers.OrderBy(c => c.CustomerID)
            .ToPageAsync(count, null, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(count, all.Values.Count);
        Assert.Equal("ALFKI", all.Values[0].CustomerID);
        Assert.Equal("WOLZA", all.Values[^1].CustomerID);
        Assert.Null(all.ContinuationToken);
        var filtered = await customers.Where(c => c.CustomerID == "ALFKI")
            .ToPageAsync(10, null, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("ALFKI", Assert.Single(filtered.Values).CustomerID);
        Assert.Null(filtered.ContinuationToken);
#pragma warning restore EF9102
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Distinct_projection_refuses_translation_in_the_direct_store(bool async)
    {
        using var context = ((InfoCarrierTestStore)fixture.TestStore).Backend.CreateDbContext();
        var query = context.Set<Microsoft.EntityFrameworkCore.TestModels.Northwind.Customer>()
            .OrderBy(c => c.CustomerID)
            .Select(c => new { c.CustomerID, FirstLetter = c.CustomerID.Substring(0, 1), Foo = "Foo" })
            .Distinct()
            .Select(x => new { Aggregate = x.FirstLetter + " " + x.Foo });
        InvalidOperationException error = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync(TestContext.Current.CancellationToken))
            : Assert.Throws<InvalidOperationException>(() => query.ToList());
        Assert.Contains("could not be translated", error.Message, StringComparison.Ordinal);
        Assert.Contains(".Distinct()", error.Message, StringComparison.Ordinal);
    }
}
