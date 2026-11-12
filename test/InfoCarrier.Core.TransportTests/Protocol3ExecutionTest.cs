// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Wire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;
using Old = InfoCarrier.Core.Wire.V3.Common;
using OldFrames = InfoCarrier.Core.Wire.V3.Query;

namespace InfoCarrier.Core.TransportTests;

public class Protocol3ExecutionTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task Historical_queries_match_the_database_including_raw_query_roots()
    {
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddInfoCarrierServer<NorthwindContext>(options => options.AllowArbitrarySqlExecution(true))));
        using HttpClient http = host.CreateClient();
        var serializer = new Protocol3Serializer();
        await using NorthwindContext client = CreateContext(http, serializer, allowSql: true);
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        NorthwindContext server = scope.ServiceProvider.GetRequiredService<NorthwindContext>();

        static IQueryable<string> Query(NorthwindContext context) => context.Products
            .Where(product => product.Id > 1 && !product.Name.StartsWith("Z"))
            .OrderBy(product => product.Id).Take(3)
            .Select(product => product.Name.ToUpper() + ":" + (product.UnitsInStock + 2));

        string[] expected = await Query(server).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(expected);
        Assert.Equal(expected, await Query(client).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(await server.Products.FromSqlRaw("SELECT * FROM Products WHERE Id > {0}", 1)
                .OrderBy(product => product.Id).Select(product => product.Id).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken),
            await client.Products.FromSqlRaw("SELECT * FROM Products WHERE Id > {0}", 1)
                .OrderBy(product => product.Id).Select(product => product.Id).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(await server.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Products").ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken),
            await client.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Products").ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3, serializer.Operations.Count);
        Assert.All(serializer.Operations, operation => Assert.Equal(Old.InfoCarrierOperation.Query, operation));
    }

    [Fact]
    public async Task Historical_conversion_does_not_grant_server_raw_query_permission()
    {
        var counter = new ReadCounter();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IInterceptor>(counter)));
        using HttpClient http = host.CreateClient();
        var serializer = new Protocol3Serializer();
        await using NorthwindContext client = CreateContext(http, serializer, allowSql: true);
        int before = counter.Reads;

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.Products.FromSqlRaw("SELECT * FROM Products").ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("SQL", failure.Message, StringComparison.Ordinal);
        Assert.Equal(Old.InfoCarrierOperation.Query, Assert.Single(serializer.Operations));
        Assert.Equal(before, counter.Reads);
    }

    [Fact]
    public async Task Historical_stream_frames_keep_the_buffered_query_values()
    {
        using HttpClient http = factory.CreateClient();
        var serializer = new Protocol3Serializer();
        await using NorthwindContext client = CreateContext(http, serializer);
        int[] expected = await client.Products.OrderBy(product => product.Id).Select(product => product.Id).Take(3).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "infocarrier")
        {
            Content = new ByteArrayContent(serializer.LastQueryBody!),
        };
        request.Headers.Add("InfoCarrier-Stream-Operation", "Query");
        request.Headers.Add("InfoCarrier-Protocol-Version", "3");
        using HttpResponseMessage response = await http.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream source = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        var frames = new List<OldFrames.QueryStreamFrame>();
        await foreach (OldFrames.QueryStreamFrame frame in OldFrames.QueryStreamFrameCodec.ReadAsync(source, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }
        Assert.Equal(3, Assert.IsType<OldFrames.QueryStartFrame>(frames[0]).Version);
        Assert.IsType<OldFrames.QueryCompleteFrame>(frames[^1]);
        Assert.Empty(frames.OfType<OldFrames.QueryFaultFrame>());
        Assert.Equal(expected, frames.OfType<OldFrames.QueryRowFrame>()
            .Select(frame => Assert.IsType<JsonElement>(frame.Value.PrimitiveValue).GetInt32()));
    }

    [Fact]
    public async Task Historical_save_failure_keeps_failed_entry_identity_and_atomicity()
    {
        using HttpClient http = factory.CreateClient();
        var serializer = new Protocol3Serializer();
        await using NorthwindContext client = CreateContext(http, serializer);
        string id = Guid.NewGuid().ToString("N")[..5];
        client.Customers.Add(new Customer { Id = id, CompanyName = "New", City = "A", Country = "X" });
        client.Customers.Add(new Customer { Id = "ALFKI", CompanyName = "Duplicate", City = "B", Country = "X" });

        DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => client.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal("ALFKI", Assert.IsType<Customer>(Assert.Single(failure.Entries).Entity).Id);
        Assert.Equal(Old.InfoCarrierOperation.SaveChanges, Assert.Single(serializer.Operations));
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        NorthwindContext database = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
        Assert.False(await database.Customers.AnyAsync(customer => customer.Id == id, cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotEqual("Duplicate", (await database.Customers.SingleAsync(customer => customer.Id == "ALFKI", cancellationToken: TestContext.Current.CancellationToken)).CompanyName);
    }

    [Fact]
    public async Task Historical_writes_preserve_generated_keys_updates_savepoints_and_rollback()
    {
        // Capability lookup is synchronous, so use the existing socket host for both execution modes.
        await using var host = NorthwindOverHttpTest.SynchronousHost(factory);
        using HttpClient http = host.CreateClient();
        var serializer = new Protocol3Serializer();
        string name = $"protocol3-{Guid.NewGuid():N}";
        int id;
        await using (NorthwindContext client = CreateContext(http, serializer))
        {
            await using IDbContextTransaction transaction = await client.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            Assert.True(transaction.SupportsSavepoints);
            var retained = new Product { Name = name, UnitPrice = 2.5m, UnitsInStock = 3, CategoryId = 1 };
            var second = new Product { Name = name + "-second", UnitPrice = 7m, UnitsInStock = 4, CategoryId = 1 };
            client.AddRange(retained, second);
            Assert.Equal(2, await client.SaveChangesAsync(TestContext.Current.CancellationToken));
            id = retained.Id;
            Assert.True(id > 0);
            Assert.True(second.Id > 0);
            Assert.NotEqual(id, second.Id);

            await transaction.CreateSavepointAsync("retained", TestContext.Current.CancellationToken);
            client.Products.Add(new Product { Name = name + "-discarded", CategoryId = 1 });
            Assert.Equal(1, await client.SaveChangesAsync(TestContext.Current.CancellationToken));
            await transaction.RollbackToSavepointAsync("retained", TestContext.Current.CancellationToken);
            await transaction.ReleaseSavepointAsync("retained", TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            NorthwindContext database = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
            Product retained = await database.Products.SingleAsync(product => product.Id == id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(name, retained.Name);
            Assert.Equal(2.5m, retained.UnitPrice);
            Assert.Equal(3, retained.UnitsInStock);
            Assert.Equal(7m, (await database.Products.SingleAsync(product => product.Name == name + "-second", cancellationToken: TestContext.Current.CancellationToken)).UnitPrice);
            Assert.False(await database.Products.AnyAsync(product => product.Name == name + "-discarded", cancellationToken: TestContext.Current.CancellationToken));
        }

        await using (NorthwindContext client = CreateContext(http, serializer))
        {
            Product product = await client.Products.SingleAsync(product => product.Id == id, cancellationToken: TestContext.Current.CancellationToken);
            product.UnitsInStock = 19;
            Assert.Equal(1, await client.SaveChangesAsync(TestContext.Current.CancellationToken));
            await using IDbContextTransaction transaction = await client.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            product.Name = name + "-rolled-back";
            Assert.Equal(1, await client.SaveChangesAsync(TestContext.Current.CancellationToken));
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            NorthwindContext database = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
            Product retained = await database.Products.SingleAsync(product => product.Id == id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(name, retained.Name);
            Assert.Equal(19, retained.UnitsInStock);
        }

        await using (NorthwindContext client = CreateContext(http, serializer))
        {
            client.Products.Remove(await client.Products.SingleAsync(product => product.Id == id, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(1, await client.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            NorthwindContext database = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
            Assert.False(await database.Products.AnyAsync(product => product.Id == id, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (Old.InfoCarrierOperation operation in Enum.GetValues<Old.InfoCarrierOperation>())
        {
            Assert.Contains(operation, serializer.Operations);
        }
    }

    [Fact]
    public async Task A_historical_request_keeps_backend_translation_refusal_without_local_retry()
    {
        using HttpClient http = factory.CreateClient();
        var serializer = new Protocol3Serializer();
        await using NorthwindContext client = CreateContext(http, serializer);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        NorthwindContext server = scope.ServiceProvider.GetRequiredService<NorthwindContext>();

        static Task<Product[]> Query(NorthwindContext context) => context.Products
            .Where(product => product.Name.StartsWith("x", StringComparison.Ordinal)).ToArrayAsync();

        InvalidOperationException expected = await Assert.ThrowsAsync<InvalidOperationException>(() => Query(server));
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Query(client));
        Assert.Contains("could not be translated", expected.Message, StringComparison.Ordinal);
        Assert.Contains("could not be translated", actual.Message, StringComparison.Ordinal);
        Assert.Equal(Old.InfoCarrierOperation.Query, Assert.Single(serializer.Operations));
    }

    private static NorthwindContext CreateContext(HttpClient http, IInfoCarrierSerializer serializer, bool allowSql = false)
        => new(new DbContextOptionsBuilder<NorthwindContext>()
            .UseInfoCarrier(new TransportInfoCarrierClient(new BufferedTransport(new HttpInfoCarrierTransport(http, serializer)), serializer),
                options => options.AllowArbitrarySqlExecution(allowSql))
            .UseLazyLoadingProxies().Options);

    // Buffered requests use the serializer's historical envelope. Streaming is tested explicitly
    // above with its historical header, rather than the current client's version 4 header.
    private sealed class BufferedTransport(IInfoCarrierTransport inner) : IInfoCarrierTransport
    {
        public InfoCarrierEnvelope Send(InfoCarrierEnvelope request) => inner.Send(request);
        public Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => inner.SendAsync(request, cancellationToken);
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Reads { get; private set; }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Reads++;
            return result;
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult(result);
        }
    }

    // Exercise the shipped historical codecs through a current client shell. Only the shell's
    // envelope admission uses version 4; every HTTP request and response is independently checked
    // as version 3. This is codec acceptance coverage, not an unchanged published-client test.
    private sealed class Protocol3Serializer : IInfoCarrierSerializer
    {
        private readonly InfoCarrierWireProtocol _protocols = new([new CurrentWireProtocolCodec(), new Protocol3WireProtocolCodec()]);
        private readonly IInfoCarrierSerializer _serializer;
        public List<Old.InfoCarrierOperation> Operations { get; } = [];
        public byte[]? LastQueryBody { get; private set; }

        public Protocol3Serializer()
            => _serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, _protocols);

        public byte[] Serialize<T>(T value)
        {
            if (value is InfoCarrierEnvelope envelope)
            {
                byte[] body = _protocols.Serialize(_serializer, envelope with { ProtocolVersion = 3 }, 3);
                Old.InfoCarrierEnvelope old = _serializer.Deserialize<Old.InfoCarrierEnvelope>(body)!;
                Assert.Equal(3, old.ProtocolVersion);
                Operations.Add(old.Operation);
                return body;
            }
            if (value is QueryDataRequest)
            {
                byte[] body = _protocols.Serialize(_serializer, value, 3);
                LastQueryBody = body;
                using JsonDocument document = JsonDocument.Parse(body);
                Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("serializedQuery").ValueKind);
                Assert.False(document.RootElement.TryGetProperty("query", out _));
                return body;
            }
            return _protocols.Serialize(_serializer, value, 3);
        }

        public T? Deserialize<T>(byte[] payload)
        {
            if (typeof(T) != typeof(InfoCarrierEnvelope)) { return _protocols.Deserialize<T>(_serializer, payload, 3); }
            using var source = new MemoryStream(payload);
            // A completed in-memory decode, not synchronous waiting for remote work.
            InfoCarrierEnvelope response = _protocols.ReadEnvelopeAsync(_serializer, source, default).GetAwaiter().GetResult()!;
            Assert.Equal(3, response.ProtocolVersion);
            return (T)(object)(response with { ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion });
        }

        public ValueTask<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Serialize(value));
        public ValueTask<T?> DeserializeAsync<T>(byte[] payload, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Deserialize<T>(payload));
    }
}
