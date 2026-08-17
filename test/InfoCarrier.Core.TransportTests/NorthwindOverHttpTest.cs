// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
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

        // Every German customer the seed defines, in id order. An exact set rather than a count:
        // a Where that silently matched everything would still produce a plausible number.
        Assert.Equal(
            ["ALFKI", "BLAUS", "DRACD", "FRANK", "KOENE", "LEHMS", "OTTIK", "WANDK"],
            customers.Select(c => c.Id));
    }

    [Fact]
    public async Task A_projection_crosses_as_columns_rather_than_as_entities()
    {
        using NorthwindContext context = CreateClientContext(factory, out RecordingHandler recorder);

        var rows = await context.Orders
            .OrderBy(o => o.Id)
            .Select(o => new { o.Id, o.CustomerId })
            .ToListAsync();

        Assert.Equal(NorthwindSeed.OrderCount, rows.Count);
        Assert.Equal("ALFKI", rows[0].CustomerId);

        // A defect that shipped whole Order entities and projected client-side would produce the
        // same five rows above -- the values alone cannot tell the two apart. What distinguishes
        // them is OrderDate, which the projection excludes and a whole-entity payload would carry.
        // Every seeded Order has a distinct date (NorthwindSeed.cs), so its ISO date part is a
        // concrete, falsifiable fingerprint: it appears in the payload if and only if OrderDate
        // rode along. System.Text.Json's default DateTime converter always emits the date part in
        // "yyyy-MM-dd" form. The five anchor orders are enough to detect it: if OrderDate rode
        // along at all it rode along for every row, so catching any one of them is catching all.
        //
        // **The search is against the response body itself now, and that is D7 half (A) showing
        // through.** It used to have to decode two base64 layers first: the recorded bytes were an
        // InfoCarrierEnvelope whose Payload was a byte[] holding a QueryDataResult, whose
        // SerializedResults was *again* a byte[] holding the rows. The base64 alphabet contains no
        // '-', so a hyphenated date could not appear at either outer layer no matter what the rows
        // carried, and an assertion against the raw body could not fail -- which Phase 1 learned
        // the expensive way. A streamed query response has no nesting left: the rows are JSON
        // objects in a top-level JSON array, so their values are in the body as text.
        string[] seededOrderDates = ["2026-01-05", "2026-02-11", "2026-02-18", "2026-03-02", "2026-03-20"];
        string allRowData = string.Concat(
            recorder.ResponseBodies.Select(System.Text.Encoding.UTF8.GetString));

        // The premise the search rests on, asserted rather than assumed: the dates are absent
        // because the projection excluded them, not because the body is opaque to this search. A
        // value the projection *did* ask for has to be findable by the very same means, or the
        // assertions below prove nothing -- which is exactly the trap the old two-layer form fell
        // into.
        Assert.Contains("ALFKI", allRowData);

        foreach (string orderDate in seededOrderDates)
        {
            Assert.DoesNotContain(orderDate, allRowData);
        }
    }

    [Fact]
    public async Task An_aggregate_is_answered_by_the_server()
    {
        using NorthwindContext context = CreateClientContext(factory, out RecordingHandler recorder);

        int count = await context.OrderDetails.CountAsync(d => d.Quantity >= 10);

        Assert.Equal(330, count);

        // A client-side count (fetch every matching OrderDetail, then Count() locally) would reach
        // the same answer as a server-side COUNT -- the value alone cannot tell them apart. Two
        // things a client-side count cannot avoid: a second request to fetch the rows, and a
        // payload big enough to carry them. One request proves the query was not first materialized
        // and then re-queried; response size distinguishes "returned a number" from "returned rows".
        // Bound chosen empirically: the envelope carrying one int measures 448 bytes. The
        // alternative is 330 matching OrderDetail rows of four columns each, which is orders of
        // magnitude past this bound -- so 700 sits above the measured scalar size with headroom
        // and far below anything row-shaped.
        Assert.Equal(1, recorder.RequestCount);
        Assert.Single(recorder.ResponseBodies);
        Assert.True(
            recorder.ResponseBodies[0].Length < 700,
            $"Expected a scalar-sized response, got {recorder.ResponseBodies[0].Length} bytes.");
    }

    [Fact]
    public async Task Touching_a_navigation_lazy_loads_it_over_a_second_round_trip()
    {
        using NorthwindContext context = CreateClientContext(factory, out RecordingHandler recorder);

        Order order = await context.Orders.SingleAsync(o => o.Id == 1);

        // The initial query asked for orders and nothing else. A defect that eagerly over-fetched
        // Customer and OrderDetails during this call would make every assertion below pass while
        // falsifying the test's name -- only the request count can tell "populated already" apart
        // from "populated because it was touched".
        Assert.Equal(1, recorder.RequestCount);

        // The query asked for orders and nothing else, so the navigation is empty until it is
        // read. Reading it is what issues the second request.
        Customer? customer = order.Customer;

        Assert.NotNull(customer);
        Assert.Equal("ALFKI", customer.Id);
        Assert.True(
            recorder.RequestCount > 1,
            "Reading order.Customer should have issued a second round trip.");
        int requestCountAfterCustomer = recorder.RequestCount;

        Assert.Equal(2, order.OrderDetails.Count);
        Assert.True(
            recorder.RequestCount > requestCountAfterCustomer,
            "Enumerating order.OrderDetails should have issued a further round trip.");
    }

    internal static NorthwindContext CreateClientContext(NorthwindServerFactory factory)
        => CreateClientContext(factory, out _);

    internal static NorthwindContext CreateClientContext(NorthwindServerFactory factory, out RecordingHandler recorder)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var handler = new RecordingHandler();
        HttpClient httpClient = factory.CreateDefaultClient(handler);
        recorder = handler;

        var client = new TransportInfoCarrierClient(
            new HttpInfoCarrierTransport(httpClient, serializer), serializer);

        return new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseInfoCarrier(client)
                .UseLazyLoadingProxies()
                .Options);
    }
}
