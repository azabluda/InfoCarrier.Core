// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Context = InfoCarrier.Core.TransportTests.ClientProcessingLoggingTest.Context;
using Logs = InfoCarrier.Core.TransportTests.ClientProcessingLoggingTest.Logs;

namespace InfoCarrier.Core.TransportTests;

public class ChainedClientLoggingTest
{
    [Fact]
    public void Capture_instances_are_isolated_and_disposal_stops_collection()
    {
        using var first = new ServerLogCapture();
        using var second = new ServerLogCapture();
        ILogger firstLogger = first.CreateLogger("Store.First");
        ILogger secondLogger = second.CreateLogger("Store.Second");

        ServerLogCapture.Scope scope = first.Begin(LogLevel.Warning);
        firstLogger.LogWarning(41, "first");
        secondLogger.LogWarning(42, "second");
        Assert.Equal("first", Assert.Single(scope.Events!).Message);

        scope.Dispose();
        firstLogger.LogWarning(43, "late");
        Assert.Equal("first", Assert.Single(scope.Events!).Message);
    }

    [Fact]
    public async Task Concurrent_capture_scopes_do_not_exchange_events()
    {
        using var capture = new ServerLogCapture();
        ILogger logger = capture.CreateLogger("Store.Concurrent");
        IReadOnlyList<ServerLogEvent>?[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            using ServerLogCapture.Scope scope = capture.Begin(LogLevel.Warning);
            logger.LogWarning(i, "event-{Index}", i);
            return scope.Events;
        })));

        for (int i = 0; i < results.Length; i++)
        {
            ServerLogEvent item = Assert.Single(results[i]!);
            Assert.Equal(i, item.EventId);
            Assert.Equal($"event-{i}", item.Message);
        }
    }

    [Fact]
    public void Nested_capture_restores_the_owning_instance_scope()
    {
        using var capture = new ServerLogCapture();
        ILogger logger = capture.CreateLogger("Store.Nested");
        using ServerLogCapture.Scope outer = capture.Begin(LogLevel.Warning);
        logger.LogWarning(1, "outer-before");
        using (ServerLogCapture.Scope inner = capture.Begin(LogLevel.Warning))
        {
            logger.LogWarning(2, "inner");
            Assert.Equal(2, Assert.Single(inner.Events!).EventId);
        }
        logger.LogWarning(3, "outer-after");
        Assert.Equal([1, 3], outer.Events!.Select(e => e.EventId));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Each_hop_controls_forwarding_and_sensitive_gate(bool middleGrant, bool downstreamGrant, bool sensitive)
    {
        using var chain = new Chain(middleGrant, downstreamGrant, sensitive);
        using var context = chain.CreateClient();
        Assert.Equal("one", Assert.Single(await context.Items.ToListAsync()).Name);
        bool downstreamCommand = chain.DownstreamResult?.ServerLog?.Any(e => e.Category == "Microsoft.EntityFrameworkCore.Database.Command") == true;
        bool upstreamCommand = chain.UpstreamResult?.ServerLog?.Any(e => e.Category == "Microsoft.EntityFrameworkCore.Database.Command") == true;
        Assert.Equal(downstreamGrant && !sensitive, downstreamCommand);
        Assert.Equal(middleGrant && downstreamGrant && !sensitive, upstreamCommand);
        Assert.DoesNotContain(chain.ClientLog.Entries, e => e.Id is 35100 or 35101 or 35102);
        Assert.DoesNotContain(chain.MiddleLog.Entries, e => e.Id is 35100 or 35101 or 35102);
    }

    [Theory]
    [InlineData("fault")]
    [InlineData("transport")]
    [InlineData("cancel")]
    [InlineData("decode")]
    [InlineData("write decode")]
    public async Task Failures_keep_the_observed_phase_at_each_hop(string mode)
    {
        using var chain = new Chain();
        using var cancellation = new CancellationTokenSource();
        if (mode == "fault") { chain.Downstream.Prepare = r => r with { Payload = [255] }; }
        if (mode is "decode" or "write decode") { chain.Downstream.Transform = r => r with { Payload = [255] }; }
        if (mode == "transport") { chain.Downstream.Sending = _ => throw new TimeoutException("secret-transport"); }
        if (mode == "cancel")
        {
            chain.Downstream.Sending = token => { cancellation.Cancel(); token.ThrowIfCancellationRequested(); };
        }
        using var context = chain.CreateClient();
        if (mode == "write decode") { context.Items.Add(new ClientProcessingLoggingTest.Item { Name = "saved" }); }
        Exception? failure = await Record.ExceptionAsync(async () =>
        {
            if (mode == "write decode") { await context.SaveChangesAsync(cancellation.Token); }
            else { await context.Items.ToListAsync(cancellation.Token); }
        });
        Assert.NotNull(failure);
        Logs.Entry middle = Assert.Single(chain.MiddleLog.Entries, e => e.Id == 35100);
        Logs.Entry upstream = Assert.Single(chain.ClientLog.Entries, e => e.Id == 35100);
        string phase = mode switch
        {
            "fault" => "returned server fault",
            "decode" or "write decode" => "response payload deserialization",
            _ => "transport exchange",
        };
        Assert.Contains(phase, middle.Message);
        Assert.Contains(mode == "cancel" ? "transport exchange" : "returned server fault", upstream.Message);
        Assert.Null(middle.Exception);
        Assert.Null(upstream.Exception);
        Assert.DoesNotContain("secret", middle.Message);
        Assert.DoesNotContain("secret", upstream.Message);
        Assert.DoesNotContain(chain.MiddleLog.Entries, e => e.Id is 35101 or 35102);
        Assert.DoesNotContain(chain.ClientLog.Entries, e => e.Id is 35101 or 35102);
        if (mode == "cancel")
        {
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.Equal(LogLevel.Debug, middle.Level);
            Assert.Equal(LogLevel.Debug, upstream.Level);
        }
        if (mode == "write decode")
        {
            chain.Downstream.Transform = r => r;
            using var verification = chain.Downstream.CreateClient();
            Assert.Equal(2, await verification.Items.CountAsync());
            Assert.DoesNotContain("rolled back", middle.Message);
        }
    }

    [Fact]
    public async Task Concurrent_chained_failures_preserve_host_scopes()
    {
        using var chain = new Chain();
        chain.Downstream.Transform = r => r with { Payload = [255] };
        await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Task.Run(async () =>
        {
            string scopeName = $"request-{i}";
            using var clientScope = chain.ClientFactory.CreateLogger("host").BeginScope(scopeName);
            using var middleScope = chain.MiddleFactory.CreateLogger("host").BeginScope(scopeName);
            using var context = chain.CreateClient();
            Assert.NotNull(await Record.ExceptionAsync(() => context.Items.ToListAsync()));
        })));
        for (int i = 0; i < 6; i++)
        {
            string expected = $"request-{i}";
            Assert.Single(chain.ClientLog.Entries, e => e.Id == 35100 && e.Scopes.Contains(expected));
            Assert.Single(chain.MiddleLog.Entries, e => e.Id == 35100 && e.Scopes.Contains(expected));
        }
        Assert.All(chain.ClientLog.Entries.Where(e => e.Id == 35100), e => Assert.Single(e.Scopes));
        Assert.All(chain.MiddleLog.Entries.Where(e => e.Id == 35100), e => Assert.Single(e.Scopes));
    }

    private sealed class Chain : IDisposable
    {
        public ClientProcessingLoggingTest.Fixture Downstream { get; }
        public Logs ClientLog { get; } = new();
        public Logs MiddleLog { get; } = new();
        public ILoggerFactory ClientFactory { get; }
        public ILoggerFactory MiddleFactory { get; }
        private readonly ServiceProvider _middle;
        private readonly InProcessInfoCarrierServer _server;
        private readonly IInfoCarrierClient _client;
        public QueryDataResult? DownstreamResult { get; private set; }
        public QueryDataResult? UpstreamResult { get; private set; }

        public Chain(bool middleGrant = false, bool downstreamGrant = false, bool sensitive = false)
        {
            Downstream = new(downstreamGrant, sensitive);
            ClientFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(ClientLog));
            var capture = new ServerLogCapture();
            MiddleFactory = LoggerFactory.Create(b =>
            {
                b.SetMinimumLevel(LogLevel.Debug).AddProvider(MiddleLog);
                if (middleGrant) { b.AddProvider(capture); }
            });
            var serializer = new SystemTextJsonInfoCarrierSerializer();
            Downstream.Transform = response =>
            {
                if (response.Fault is null && response.Operation == InfoCarrierOperation.Query)
                { DownstreamResult = serializer.Deserialize<QueryDataResult>(response.Payload); }
                return response;
            };
            var middleClient = new TransportInfoCarrierClient(Downstream.Transport, serializer,
                MiddleFactory.CreateLogger<TransportInfoCarrierClient>());
            var services = new ServiceCollection()
                .AddDbContext<Context>(b => b.UseInfoCarrier(middleClient).UseLoggerFactory(MiddleFactory))
                .AddScoped<DbContext>(s => s.GetRequiredService<Context>());
            if (middleGrant)
            {
                services.AddSingleton(capture).AddInfoCarrierServerLogForwarding(LogLevel.Debug);
            }
            _middle = services.BuildServiceProvider();
            _server = new InProcessInfoCarrierServer(
                _middle,
                MiddleFactory.CreateLogger<InProcessInfoCarrierServer>(),
                _middle.GetService<ServerLogCapture>(),
                _middle.GetService<IInfoCarrierServerLogForwarding>(),
                _middle.GetService<IInfoCarrierSensitiveServerLogForwarding>());
            var dispatcher = new InfoCarrierEnvelopeServer(_server, serializer, MiddleFactory.CreateLogger<InfoCarrierEnvelopeServer>());
            _client = new TransportInfoCarrierClient(new ClientProcessingLoggingTest.LocalTransport(dispatcher, response =>
            {
                if (response.Fault is null && response.Operation == InfoCarrierOperation.Query)
                { UpstreamResult = serializer.Deserialize<QueryDataResult>(response.Payload); }
                return response;
            }), serializer, ClientFactory.CreateLogger<TransportInfoCarrierClient>());
        }

        public Context CreateClient() => new(new DbContextOptionsBuilder<Context>()
            .UseInfoCarrier(_client).UseLoggerFactory(ClientFactory).Options);

        public void Dispose()
        {
            _server.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _middle.Dispose();
            MiddleFactory.Dispose();
            ClientFactory.Dispose();
            Downstream.Dispose();
        }
    }
}
