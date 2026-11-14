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
