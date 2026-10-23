// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Northwind.Shared;
using Northwind.Shared.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using InfoCarrier.Core.Expressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     The premise of the whole product, asserted over a real HTTP hop: a DbContext with no
///     database answers questions about data it cannot reach.
/// </summary>
public class NorthwindOverHttpTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public void A_console_style_synchronous_query_crosses_HTTP()
    {
        using WebApplicationFactory<Program> hosted = SynchronousHost(factory);
        using HttpClient httpClient = hosted.CreateClient();
        using NorthwindContext context = CreateClientContext(httpClient);

        List<string> customers = context.Customers
            .Where(customer => customer.Country == "Germany")
            .OrderBy(customer => customer.Id)
            .Select(customer => customer.Id)
            .ToList();

        Assert.Equal(
            ["ALFKI", "BLAUS", "DRACD", "FRANK", "KOENE", "LEHMS", "OTTIK", "WANDK"],
            customers);
    }

    internal static WebApplicationFactory<Program> SynchronousHost(NorthwindServerFactory factory)
    {
        WebApplicationFactory<Program> hosted = factory.WithWebHostBuilder(
            builder => builder.ConfigureServices(
                services => services.AddLogging(logging => logging.ClearProviders())));
        hosted.UseKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));
        return hosted;
    }

    internal static NorthwindContext CreateClientContext(HttpClient httpClient)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var client = new TransportInfoCarrierClient(
            new HttpInfoCarrierTransport(httpClient, serializer), serializer);
        return new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseInfoCarrier(client)
                .UseLazyLoadingProxies()
                .Options);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Shared_deployment_obeys_all_anonymous_registration_states(
        bool clientShape,
        bool serverShape)
    {
        var prototype = new { Key = (string?)null, Count = 0 };
        var counter = new CommandCounter();

        void Configure(InfoCarrierDeploymentBuilder builder)
            => builder.AddAnonymousShapes(prototype);

        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<AnonymousShapeCatalog>();
            if (serverShape)
            {
                services.AddInfoCarrier(Configure);
            }

            services.AddSingleton<IInterceptor>(counter);
        }));
        using HttpClient warmup = configured.CreateClient();
        int beforeQuery = counter.ReaderExecutions;
        using NorthwindContext context = CreateClientContext(
            configured,
            registerShape: false,
            out RecordingHandler recorder,
            deployment: clientShape ? Configure : null);
        var query = context.Orders.GroupBy(order => order.CustomerId)
            .Select(group => new { group.Key, Count = group.Count() });

        if (clientShape && !serverShape)
        {
            InvalidOperationException refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync());
            Assert.Contains("catalog", refusal.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeQuery, counter.ReaderExecutions);
        }
        else
        {
            var rows = await query.ToListAsync();
            Assert.Equal(NorthwindSeed.OrderCount, rows.Sum(row => row.Count));
            Assert.All(rows, row => Assert.Equal(prototype.GetType(), row.GetType()));
            Assert.True(counter.ReaderExecutions > beforeQuery);
        }

        Assert.Equal(1, recorder.RequestCount);
    }

    [Fact]
    public async Task Client_shape_calls_compose_snapshot_inputs_and_remain_context_scoped()
    {
        var counter = new CommandCounter();
        await using var unconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<AnonymousShapeCatalog>();
            services.AddSingleton<IInterceptor>(counter);
        }));
        using HttpClient warmup = unconfigured.CreateClient();
        int beforeQuery = counter.ReaderExecutions;
        object[] shapes = [new { Code = (string?)null }];
        using NorthwindContext context = CreateClientContext(unconfigured, registerShape: true,
            out RecordingHandler recorder, anonymousShapes: shapes);
        shapes[0] = new { Other = 0 };

        InvalidOperationException first = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Orders
            .Select(order => new { order.Id, order.CustomerId }).ToListAsync());
        InvalidOperationException second = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Orders
            .Select(order => new { Code = order.CustomerId }).ToListAsync());

        Assert.Contains("catalog", first.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("catalog", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, recorder.RequestCount);
        Assert.Equal(beforeQuery, counter.ReaderExecutions);

        using NorthwindContext undeclared = CreateClientContext(unconfigured, registerShape: false, out RecordingHandler other);
        var rows = await undeclared.Orders.Select(order => new { Code = order.CustomerId }).ToListAsync();
        Assert.Equal(NorthwindSeed.OrderCount, rows.Count);
        Assert.Equal(1, other.RequestCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Group_aggregate_projection_obeys_both_registration_states(bool clientShape, bool serverShape)
    {
        var prototype = new { Key = (string?)null, Count = 0 };
        var counter = new CommandCounter();
        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<AnonymousShapeCatalog>();
            if (serverShape)
            {
                services.AddInfoCarrier(builder => builder.AddAnonymousShapes(prototype));
            }

            services.AddSingleton<IInterceptor>(counter);
        }));
        using HttpClient warmup = configured.CreateClient();
        int beforeQuery = counter.ReaderExecutions;
        using NorthwindContext context = CreateClientContext(configured, registerShape: false,
            out RecordingHandler recorder, anonymousShapes: clientShape ? [prototype] : []);
        var query = context.Orders.GroupBy(order => order.CustomerId)
            .Select(group => new { group.Key, Count = group.Count() });

        if (clientShape && !serverShape)
        {
            InvalidOperationException refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync());
            Assert.Contains("catalog", refusal.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeQuery, counter.ReaderExecutions);
        }
        else
        {
            var rows = await query.ToListAsync();
            Assert.Equal(NorthwindSeed.OrderCount, rows.Sum(row => row.Count));
            Assert.All(rows, row => Assert.Equal(prototype.GetType(), row.GetType()));
            Assert.True(counter.ReaderExecutions > beforeQuery);
        }

        Assert.Equal(1, recorder.RequestCount);
    }

    [Fact]
    public async Task A_projection_declared_on_neither_side_is_reconstructed_on_the_client()
    {
        await using var unconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.RemoveAll<AnonymousShapeCatalog>()));
        using NorthwindContext context = CreateClientContext(unconfigured, registerShape: false, out RecordingHandler recorder);

        var rows = await context.Orders.OrderBy(o => o.Id)
            .Select(o => new { o.Id, o.CustomerId }).ToListAsync();

        Assert.Equal(NorthwindSeed.OrderCount, rows.Count);
        Assert.Equal("ALFKI", rows[0].CustomerId);
        Assert.Equal(1, recorder.RequestCount);
    }

    [Fact]
    public async Task A_client_only_shape_declaration_is_refused_without_database_execution_or_retry()
    {
        var counter = new CommandCounter();
        await using var unconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<AnonymousShapeCatalog>();
            services.AddSingleton<IInterceptor>(counter);
        }));
        using HttpClient warmup = unconfigured.CreateClient();
        int executionsBeforeQuery = counter.ReaderExecutions;
        using NorthwindContext context = CreateClientContext(unconfigured, registerShape: true, out RecordingHandler recorder);

        InvalidOperationException refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Orders
            .Select(o => new { o.Id, o.CustomerId }).ToListAsync());

        Assert.Contains("catalog", refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, recorder.RequestCount);
        Assert.Equal(executionsBeforeQuery, counter.ReaderExecutions);
    }

    [Fact]
    public async Task A_server_only_shape_declaration_keeps_client_reconstruction()
    {
        using NorthwindContext context = CreateClientContext(factory, registerShape: false, out RecordingHandler recorder);

        var rows = await context.Orders.OrderBy(o => o.Id)
            .Select(o => new { o.Id, o.CustomerId }).ToListAsync();

        Assert.Equal(NorthwindSeed.OrderCount, rows.Count);
        Assert.Equal("ALFKI", rows[0].CustomerId);
        Assert.Equal(1, recorder.RequestCount);
    }

    [Fact]
    public async Task Matching_shape_declarations_use_the_catalog_and_restore_the_client_type()
    {
        var prototype = new { Id = 0, CustomerId = (string?)null };
        using NorthwindContext context = CreateClientContext(factory, registerShape: true, out RecordingHandler recorder);

        var rows = await context.Orders.OrderBy(o => o.Id)
            .Select(o => new { o.Id, o.CustomerId }).ToListAsync();

        Assert.Equal(NorthwindSeed.OrderCount, rows.Count);
        Assert.Equal(prototype.GetType(), rows[0].GetType());
        Assert.Equal("ALFKI", rows[0].CustomerId);
        Assert.Equal(1, recorder.RequestCount);
    }

    [Fact]
    public async Task Matching_nested_shape_declarations_restore_both_client_types()
    {
        var prototype = new { Id = 0, Customer = new { CustomerId = (string?)null } };
        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddInfoCarrier(builder => builder.AddAnonymousShapes(prototype))));
        using NorthwindContext context = CreateClientContext(
            configured,
            registerShape: false,
            out RecordingHandler recorder,
            anonymousShapes: [prototype]);

        var rows = await context.Orders.OrderBy(order => order.Id).Select(order => new
        {
            order.Id,
            Customer = new { order.CustomerId },
        }).ToListAsync();

        Assert.Equal(NorthwindSeed.OrderCount, rows.Count);
        Assert.Equal(prototype.GetType(), rows[0].GetType());
        Assert.Equal(prototype.Customer.GetType(), rows[0].Customer.GetType());
        Assert.Equal("ALFKI", rows[0].Customer.CustomerId);
        Assert.Equal(1, recorder.RequestCount);
    }

    [Fact]
    public async Task A_registered_shape_refused_by_sqlite_is_not_retried_on_the_client()
    {
        var counter = new CommandCounter();
        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IInterceptor>(counter)));
        using HttpClient warmup = configured.CreateClient();
        int executionsBeforeQuery = counter.ReaderExecutions;
        using NorthwindContext context = CreateClientContext(
            configured, registerShape: true, out RecordingHandler recorder);

        InvalidOperationException refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Orders
            .OrderBy(order => new { order.Id, order.CustomerId }).ToListAsync());

        Assert.Contains("translated", refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, recorder.RequestCount);
        Assert.Equal(executionsBeforeQuery, counter.ReaderExecutions);
    }

    [Fact]
    public async Task An_unregistered_record_projection_is_reconstructed_on_the_client()
    {
        using NorthwindContext context = CreateClientContext(
            factory, registerShape: false, out RecordingHandler recorder, allowRecord: false);

        List<OrderCard> rows = await context.Orders.OrderBy(order => order.Id)
            .Select(order => new OrderCard(order.Id, order.CustomerId)).ToListAsync();

        Assert.Equal(NorthwindSeed.OrderCount, rows.Count);
        Assert.Equal("ALFKI", rows[0].CustomerId);
        Assert.Equal(1, recorder.RequestCount);
    }

    [Fact]
    public async Task A_client_only_record_declaration_is_refused_without_database_execution()
    {
        var counter = new CommandCounter();
        await using var unconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IInterceptor>(counter)));
        using HttpClient warmup = unconfigured.CreateClient();
        int executionsBeforeQuery = counter.ReaderExecutions;
        using NorthwindContext context = CreateClientContext(
            unconfigured, registerShape: false, out RecordingHandler recorder, allowRecord: true);

        InvalidOperationException refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Orders
            .Select(order => new OrderCard(order.Id, order.CustomerId)).ToListAsync());

        Assert.Contains("allowed", refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, recorder.RequestCount);
        Assert.Equal(executionsBeforeQuery, counter.ReaderExecutions);
    }

    [Fact]
    public async Task Matching_record_declarations_use_backend_execution()
    {
        var counter = new CommandCounter();
        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddInfoCarrier(o => o.AddAllowedTypes(typeof(OrderCard)));
            services.AddSingleton<IInterceptor>(counter);
        }));
        using HttpClient warmup = configured.CreateClient();
        int executionsBeforeQuery = counter.ReaderExecutions;
        using NorthwindContext context = CreateClientContext(
            configured, registerShape: false, out RecordingHandler recorder, allowRecord: true);

        List<OrderCard> rows = await context.Orders.OrderBy(order => order.Id)
            .Select(order => new OrderCard(order.Id, order.CustomerId)).ToListAsync();

        Assert.Equal(NorthwindSeed.OrderCount, rows.Count);
        Assert.Equal("ALFKI", rows[0].CustomerId);
        Assert.Equal(1, recorder.RequestCount);
        Assert.True(counter.ReaderExecutions > executionsBeforeQuery);
    }

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
        // The recorded response body is not that JSON directly, though, and is
        // base64 two layers deep. The recorded bytes are the *outer* InfoCarrierEnvelope; its
        // Payload is itself a byte[], which System.Text.Json (with no converter registered for it
        // here) renders as base64. Decoding that yields the JSON for a QueryDataResult, whose
        // SerializedResults is *again* a byte[] -- the row data actually lives one base64 layer
        // further in than the envelope. The base64 alphabet contains no '-', so a hyphenated date
        // can never appear at either outer layer regardless of what the rows carry; both layers
        // must be decoded before the search means anything.
        string[] seededOrderDates = ["2026-01-05", "2026-02-11", "2026-02-18", "2026-03-02", "2026-03-20"];
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        string allRowData = string.Concat(recorder.ResponseBodies.Select(ResponseRows));
        foreach (string orderDate in seededOrderDates)
        {
            Assert.DoesNotContain(orderDate, allRowData);
        }

        string ResponseRows(byte[] bytes)
        {
            if (bytes.Length > 0 && bytes[0] == (byte)'[')
            {
                return System.Text.Encoding.UTF8.GetString(bytes);
            }

            InfoCarrierEnvelope? envelope = serializer.Deserialize<InfoCarrierEnvelope>(bytes);
            QueryDataResult? result = envelope is null
                ? null
                : serializer.Deserialize<QueryDataResult>(envelope.Payload);
            return result is null
                ? string.Empty
                : System.Text.Encoding.UTF8.GetString(result.SerializedResults);
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
        using WebApplicationFactory<Program> hosted = SynchronousHost(factory);
        using HttpClient endpoint = hosted.CreateClient();
        using var recorder = new RecordingHandler { InnerHandler = new HttpClientHandler() };
        using var httpClient = new HttpClient(recorder) { BaseAddress = endpoint.BaseAddress };
        using NorthwindContext context = CreateClientContext(httpClient);

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

    internal static NorthwindContext CreateClientContext(WebApplicationFactory<Program> factory)
        => CreateClientContext(factory, out _);

    internal static NorthwindContext CreateClientContext(WebApplicationFactory<Program> factory, out RecordingHandler recorder)
        => CreateClientContext(factory, registerShape: false, out recorder);

    internal static NorthwindContext CreateClientContext(
        WebApplicationFactory<Program> factory,
        bool registerShape,
        out RecordingHandler recorder,
        bool allowRecord = false,
        object[]? anonymousShapes = null,
        Action<InfoCarrierDeploymentBuilder>? deployment = null)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var handler = new RecordingHandler();
        HttpClient httpClient = factory.CreateDefaultClient(handler);
        recorder = handler;

        var client = new TransportInfoCarrierClient(
            new HttpInfoCarrierTransport(httpClient, serializer), serializer);

        return new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseInfoCarrier(client, options =>
                {
                    if (deployment is not null)
                    {
                        deployment(options);
                    }

                    if (registerShape)
                    {
                        options.AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null });
                    }

                    if (anonymousShapes is not null)
                    {
                        options.AddAnonymousShapes(anonymousShapes);
                    }

                    if (allowRecord)
                    {
                        options.AddAllowedTypes(typeof(OrderCard));
                    }
                })
                .UseLazyLoadingProxies()
                .Options);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int ReaderExecutions { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ReaderExecutions++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderExecutions++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed record OrderCard(int Id, string? CustomerId);
}
