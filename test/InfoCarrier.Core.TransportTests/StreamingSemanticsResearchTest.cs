// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Northwind.Shared;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class StreamingSemanticsResearchTest(NorthwindServerFactory factory)
    : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task Plain_ef_fixup_progresses_during_async_enumeration()
    {
        using HttpClient warmup = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        await using NorthwindContext context = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
        context.ChangeTracker.LazyLoadingEnabled = false;

        var query = context.Orders
            .Where(order => order.Id == 1)
            .SelectMany(
                order => order.OrderDetails,
                (order, detail) => new { Order = order, Detail = detail })
            .OrderBy(row => row.Detail.ProductId)
            .AsAsyncEnumerable();

        await using var rows = query.GetAsyncEnumerator();

        Assert.True(await rows.MoveNextAsync());
        dynamic first = rows.Current;
        Assert.Single(first.Order.OrderDetails);

        Assert.True(await rows.MoveNextAsync());
        dynamic second = rows.Current;
        Assert.Same(first.Order, second.Order);
        Assert.Equal(2, first.Order.OrderDetails.Count);

        Assert.False(await rows.MoveNextAsync());
    }
}
