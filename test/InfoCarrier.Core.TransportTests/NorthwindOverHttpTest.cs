using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;
using Northwind.Client.Transport;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     The premise of the whole product, asserted over a real HTTP hop: a DbContext with no
///     database answers questions about data it cannot reach.
/// </summary>
public class NorthwindOverHttpTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task A_client_with_no_database_reads_rows_over_http()
    {
        using NorthwindContext context = CreateClientContext(factory);

        List<Customer> customers = await context.Customers
            .Where(c => c.Country == "Germany")
            .OrderBy(c => c.Id)
            .ToListAsync();

        Assert.Equal(["ALFKI"], customers.Select(c => c.Id));
    }

    [Fact]
    public async Task A_projection_crosses_as_columns_rather_than_as_entities()
    {
        using NorthwindContext context = CreateClientContext(factory);

        var rows = await context.Orders
            .OrderBy(o => o.Id)
            .Select(o => new { o.Id, o.CustomerId })
            .ToListAsync();

        Assert.Equal(5, rows.Count);
        Assert.Equal("ALFKI", rows[0].CustomerId);
    }

    [Fact]
    public async Task An_aggregate_is_answered_by_the_server()
    {
        using NorthwindContext context = CreateClientContext(factory);

        int count = await context.OrderDetails.CountAsync(d => d.Quantity >= 10);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Touching_a_navigation_lazy_loads_it_over_a_second_round_trip()
    {
        using NorthwindContext context = CreateClientContext(factory);

        Order order = await context.Orders.SingleAsync(o => o.Id == 1);

        // The query asked for orders and nothing else, so the navigation is empty until it is
        // read. Reading it is what issues the second request.
        Customer? customer = order.Customer;

        Assert.NotNull(customer);
        Assert.Equal("ALFKI", customer.Id);
        Assert.Equal(2, order.OrderDetails.Count);
    }

    internal static NorthwindContext CreateClientContext(NorthwindServerFactory factory)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var client = new TransportInfoCarrierClient(
            new HttpInfoCarrierTransport(factory.CreateClient(), serializer), serializer);

        return new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseInfoCarrier(client)
                .UseLazyLoadingProxies()
                .Options);
    }
}
