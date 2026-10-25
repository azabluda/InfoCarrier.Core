// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using InfoCarrier.Core.AspNetCore;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class NamedEndpointPolicyIsolationTest
{
    private const string StoreCategory = "Test.NamedEndpoint.Store";

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Buffered_and_streamed_queries_keep_forwarding_and_sensitive_grants_local(
        bool sensitive, bool sensitiveGrant)
    {
        await using var host = await Host.Start(sensitive, sensitiveGrant);
        using HttpClient http = host.Http("alice", "bob");
        await Task.WhenAll(Check("orders", !sensitive || sensitiveGrant), Check("archive", false));

        async Task Check(string endpoint, bool forwarded)
        {
            var serializer = new SystemTextJsonInfoCarrierSerializer();
            var transport = new CaptureTransport(new HttpInfoCarrierTransport(http, serializer, endpoint), serializer);
            var client = new TransportInfoCarrierClient(transport, serializer);
            await using var context = new Context(new DbContextOptionsBuilder<Context>().UseInfoCarrier(client).Options);
            Assert.Equal(endpoint, (await context.Items.SingleAsync()).Name);
            AssertEvents(transport.Result!.ServerLog ?? [], endpoint, forwarded);

            // Repeat the same generated request through the streaming protocol. This checks the
            // selected endpoint's grants in both protocol paths without depending on client replay.
            await using IInfoCarrierResponse response = await ((IInfoCarrierStreamTransport)transport.Inner).SendAsync(
                InfoCarrierOperation.Query, (stream, token) => serializer.SerializeAsync(stream, transport.Request!, token));
            var events = new List<ServerLogEvent>();
            await foreach (QueryStreamFrame frame in QueryStreamFrameCodec.ReadAsync(response.Content))
            {
                Assert.IsNotType<QueryFaultFrame>(frame);
                if (frame is QueryLogFrame log) { events.Add(log.Event); }
            }
            AssertEvents(events, endpoint, forwarded);
        }
    }

    [Fact]
    public async Task Concurrent_transactions_use_each_endpoints_own_caller_selector()
    {
        await using var host = await Host.Start(false, false);
        using HttpClient owner = host.Http("alice", "bob");
        using HttpClient wrongOrders = host.Http("mallory", "alice");
        using HttpClient wrongArchive = host.Http("bob", "mallory");
        using HttpClient sameOrders = host.Http("alice", "different");
        using HttpClient sameArchive = host.Http("different", "bob");
        await Task.WhenAll(Check("orders", wrongOrders, sameOrders), Check("archive", wrongArchive, sameArchive));

        async Task Check(string endpoint, HttpClient wrong, HttpClient same)
        {
            IInfoCarrierClient client = Client(owner, endpoint);
            string token = (await client.BeginTransactionAsync()).TransactionId;
            try
            {
                var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Client(wrong, endpoint).SupportsSavepointsAsync(token));
                Assert.Contains("different caller", failure.Message);
                Assert.DoesNotContain("alice", failure.Message);
                Assert.DoesNotContain("bob", failure.Message);
                Assert.True(await Client(same, endpoint).SupportsSavepointsAsync(token));
            }
            finally { await client.RollbackTransactionAsync(token); }
        }
    }

    private static IInfoCarrierClient Client(HttpClient http, string endpoint)
        => InfoCarrierClient.Create(http, o => o.WithEndpoint("http://localhost/" + endpoint));

    private static void AssertEvents(IEnumerable<ServerLogEvent> events, string endpoint, bool forwarded)
    {
        ServerLogEvent[] storeEvents = events.Where(value => value.Category == StoreCategory).ToArray();
        if (forwarded) { Assert.Equal(endpoint, Assert.Single(storeEvents).Message); }
        else { Assert.Empty(storeEvents); }
    }

    private sealed class CaptureTransport(HttpInfoCarrierTransport inner, IInfoCarrierSerializer serializer) : IInfoCarrierTransport
    {
        internal HttpInfoCarrierTransport Inner { get; } = inner;
        internal QueryDataRequest? Request { get; private set; }
        internal QueryDataResult? Result { get; private set; }

        public InfoCarrierEnvelope Send(InfoCarrierEnvelope request)
            => Capture(request, Inner.Send(request));

        public async Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => Capture(request, await Inner.SendAsync(request, cancellationToken));

        private InfoCarrierEnvelope Capture(InfoCarrierEnvelope request, InfoCarrierEnvelope response)
        {
            if (request.Operation == InfoCarrierOperation.Query && response.Fault is null)
            {
                Request = serializer.Deserialize<QueryDataRequest>(request.Payload);
                Result = serializer.Deserialize<QueryDataResult>(response.Payload);
            }
            return response;
        }
    }

    private sealed class CommandLog(string endpoint, ILogger logger) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            logger.LogWarning(70200, "{Endpoint}", endpoint);
            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }
    }

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
    }

    public sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class Host(WebApplication app, string root) : IAsyncDisposable
    {
        internal HttpClient Http(string ordersCaller, string archiveCaller)
        {
            HttpClient http = app.GetTestClient();
            // Test headers stand in for identities established by transport authentication.
            http.DefaultRequestHeaders.Add("X-Orders-Caller", ordersCaller);
            http.DefaultRequestHeaders.Add("X-Archive-Caller", archiveCaller);
            return http;
        }

        internal static async Task<Host> Start(bool sensitive, bool sensitiveGrant)
        {
            string root = Path.Combine(Path.GetTempPath(), $"icc-endpoint-policy-{Guid.NewGuid():N}");
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            foreach (string name in new[] { "orders", "archive" })
            {
                builder.Services.AddKeyedScoped<Context>(name, (sp, _) => new Context(new DbContextOptionsBuilder<Context>()
                    .UseSqlite($"Data Source={root}-{name}.db;Pooling=false")
                    .EnableSensitiveDataLogging(sensitive)
                    .AddInterceptors(new CommandLog(name, sp.GetRequiredService<ILoggerFactory>().CreateLogger(StoreCategory)))
                    .Options));
                builder.Services.AddInfoCarrierServer<Context>(name,
                    o => o.WithLogForwarding(name == "orders" ? LogLevel.Warning : null,
                        includeSensitiveData: name == "orders" && sensitiveGrant),
                    sp => sp.GetRequiredKeyedService<Context>(name))
                    .AddHttpCallerIdentity(http => http.Request.Headers[name == "orders" ? "X-Orders-Caller" : "X-Archive-Caller"].ToString());
            }
            WebApplication app = builder.Build();
            foreach (string name in new[] { "orders", "archive" })
            {
                app.MapInfoCarrier("/" + name, serverName: name);
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                Context context = scope.ServiceProvider.GetRequiredKeyedService<Context>(name);
                await context.Database.EnsureCreatedAsync();
                context.Add(new Item { Id = 1, Name = name });
                await context.SaveChangesAsync();
            }
            await app.StartAsync();
            return new Host(app, root);
        }

        public async ValueTask DisposeAsync()
        {
            await app.DisposeAsync();
            foreach (string name in new[] { "orders", "archive" })
            {
                string path = root + "-" + name + ".db";
                File.Delete(path);
                File.Delete(path + "-wal");
                File.Delete(path + "-shm");
            }
        }
    }
}
