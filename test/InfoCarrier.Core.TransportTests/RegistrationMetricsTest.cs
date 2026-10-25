// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using InfoCarrier.Core.AspNetCore;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

[Collection(nameof(RoundTripMetricsCollection))]
public class RegistrationMetricsTest
{
    private static readonly SystemTextJsonInfoCarrierSerializer Serializer = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Named_and_unnamed_measurements_stay_distinct_in_a_dual_actor(bool clientsFirst)
    {
        string path = Path.Combine(Path.GetTempPath(), $"icc-metrics-{Guid.NewGuid():N}.db");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<Context>(o => o.UseSqlite($"Data Source={path};Pooling=false"));
        WebApplication? app = null;
        if (clientsFirst) { AddClients(); }
        InfoCarrierServerBuilder earlier = builder.Services.AddInfoCarrierServer<Context>();
        builder.Services.AddInfoCarrierServer<Context>("default");
        builder.Services.AddInfoCarrierServer<Context>("orders");
        builder.Services.AddInfoCarrierServer<Context>();
        if (!clientsFirst) { AddClients(); }
        app = builder.Build();
        try
        {
            app.MapInfoCarrier(earlier, "/earlier");
            app.MapInfoCarrier("/named", serverName: "default");
            app.MapInfoCarrier("/orders", serverName: "orders");
            app.MapInfoCarrier("/default");
            await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
            {
                Context seed = scope.ServiceProvider.GetRequiredService<Context>();
                await seed.Database.EnsureCreatedAsync();
                seed.Add(new Item { Id = 1, Name = "stored" });
                await seed.SaveChangesAsync();
            }
            await app.StartAsync();
            using var meter = new Recording();
            await Task.WhenAll(Read(null), Read("default"), Read("orders"));

            // An earlier unnamed handle remains distinct from the latest unnamed default.
            using HttpClient http = app.GetTestClient();
            await using (var context = new Context(new DbContextOptionsBuilder<Context>().UseInfoCarrier(
                InfoCarrierClient.Create(http, o => o.WithEndpoint("http://localhost/earlier"))).Options))
            {
                Assert.Equal("stored", (await context.Items.SingleAsync()).Name);
            }
            await meter.WaitForPairs(4, 4);
            var clientCounts = meter.For(InfoCarrierMetrics.RoundTripCountInstrumentName);
            var serverCounts = meter.For(InfoCarrierMetrics.ServerRequestCountInstrumentName);
            Assert.Equal(4, clientCounts.Length);
            Assert.Equal(4, serverCounts.Length);
            AssertLabel(clientCounts, InfoCarrierMetrics.ClientNameTagName, InfoCarrierMetrics.ClientRegistrationTagName, "", 4);
            AssertLabel(clientCounts, InfoCarrierMetrics.ClientNameTagName, InfoCarrierMetrics.ClientRegistrationTagName, "default", 2);
            AssertLabel(clientCounts, InfoCarrierMetrics.ClientNameTagName, InfoCarrierMetrics.ClientRegistrationTagName, "orders", 3);
            AssertLabel(serverCounts, InfoCarrierMetrics.EndpointNameTagName, InfoCarrierMetrics.EndpointRegistrationTagName, "", 1);
            AssertLabel(serverCounts, InfoCarrierMetrics.EndpointNameTagName, InfoCarrierMetrics.EndpointRegistrationTagName, "", 4);
            AssertLabel(serverCounts, InfoCarrierMetrics.EndpointNameTagName, InfoCarrierMetrics.EndpointRegistrationTagName, "default", 2);
            AssertLabel(serverCounts, InfoCarrierMetrics.EndpointNameTagName, InfoCarrierMetrics.EndpointRegistrationTagName, "orders", 3);
            Assert.All(meter.Values, reading =>
            {
                Assert.Equal(nameof(InfoCarrierOperation.Query), reading.Tags[InfoCarrierMetrics.OperationTagName]);
                Assert.Equal("success", reading.Tags[InfoCarrierMetrics.OutcomeTagName]);
                Assert.DoesNotContain(InfoCarrierMetrics.ErrorTypeTagName, reading.Tags.Keys);
                Assert.DoesNotContain("http", string.Join(",", reading.Tags.Values), StringComparison.OrdinalIgnoreCase);
            });
            meter.AssertPairs(4, 4);

            meter.Clear();
            await using (var saving = new Context(new DbContextOptionsBuilder<Context>()
                .UseApplicationServiceProvider(app.Services).UseInfoCarrier().Options))
            {
                var stub = new Item { Id = 1, Name = "updated" };
                saving.Attach(stub);
                saving.Entry(stub).Property(value => value.Name).IsModified = true;
                Assert.Equal(1, await saving.SaveChangesAsync());
            }
            await meter.WaitForPairs(1, 1);
            meter.AssertPairs(1, 1);
            Assert.All(meter.Values, reading =>
            {
                Assert.Equal(nameof(InfoCarrierOperation.SaveChanges), reading.Tags[InfoCarrierMetrics.OperationTagName]);
                Assert.Equal("success", reading.Tags[InfoCarrierMetrics.OutcomeTagName]);
            });
            await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
            {
                Context stored = scope.ServiceProvider.GetRequiredService<Context>();
                Assert.Equal("updated", (await stored.Items.SingleAsync()).Name);
            }

            IInfoCarrierClient defaultClient = app.Services.GetRequiredService<IInfoCarrierClient>();
            meter.Clear();
            Assert.NotNull(await Record.ExceptionAsync(() => defaultClient.CommitTransactionAsync("not-owned")));
            await meter.WaitForPairs(1, 1);
            meter.AssertPairs(1, 1);
            Assert.All(meter.Values, reading =>
            {
                Assert.Equal("error", reading.Tags[InfoCarrierMetrics.OutcomeTagName]);
                Assert.Contains(InfoCarrierMetrics.ErrorTypeTagName, reading.Tags.Keys);
            });

            foreach (string mode in new[] { "malformed", "version", "operation", "stream" })
            {
                meter.Clear();
                using var request = new HttpRequestMessage(HttpMethod.Post, "/default");
                if (mode == "stream")
                {
                    request.Headers.Add("InfoCarrier-Operation", nameof(InfoCarrierOperation.Query));
                    request.Content = new ByteArrayContent(Serializer.Serialize(Request()));
                }
                else
                {
                    request.Content = new ByteArrayContent(mode == "malformed" ? "{"u8.ToArray() : Serializer.Serialize(
                        new InfoCarrierEnvelope
                        {
                            ProtocolVersion = mode == "version" ? -1 : InfoCarrierEnvelope.CurrentProtocolVersion,
                            Operation = mode == "operation" ? (InfoCarrierOperation)int.MaxValue : InfoCarrierOperation.Query,
                            Payload = [],
                        }));
                }
                using HttpResponseMessage response = await http.SendAsync(request);
                if (mode == "stream")
                {
                    await using Stream content = await response.Content.ReadAsStreamAsync();
                    Assert.Contains(await QueryStreamFrameCodec.ReadAsync(content).ToListAsync(), frame => frame is QueryFaultFrame);
                }
                await meter.WaitForPairs(0, 1);
                meter.AssertPairs(0, 1);
                Assert.All(meter.Values, reading =>
                {
                    Assert.Equal("error", reading.Tags[InfoCarrierMetrics.OutcomeTagName]);
                    Assert.Equal(mode is "stream" or "version" ? "Query" : "Unknown",
                        reading.Tags[InfoCarrierMetrics.OperationTagName]);
                    Assert.Equal("", reading.Tags[InfoCarrierMetrics.EndpointNameTagName]);
                    Assert.Equal(4, reading.Tags[InfoCarrierMetrics.EndpointRegistrationTagName]);
                });
            }
        }
        finally
        {
            await app.DisposeAsync();
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }

        void AddClients()
        {
            Add(null, "earlier");
            Add("default", "named");
            Add("orders", "orders");
            Add(null, "default");
        }
        void Add(string? name, string endpoint)
        {
            Action<InfoCarrierClientOptions> configure = o => o.WithEndpoint("http://localhost/" + endpoint);
            (name is null ? builder.Services.AddInfoCarrierClient(configure) : builder.Services.AddInfoCarrierClient(name, configure))
                .ConfigurePrimaryHttpMessageHandler(() => app!.GetTestServer().CreateHandler());
        }
        async Task Read(string? name)
        {
            var options = new DbContextOptionsBuilder<Context>().UseApplicationServiceProvider(app!.Services);
            if (name is null) { options.UseInfoCarrier(); } else { options.UseInfoCarrier(name); }
            await using var context = new Context(options.Options);
            Assert.Equal("stored", (await context.Items.SingleAsync()).Name);
        }
    }

    [Theory]
    [InlineData("success", false)]
    [InlineData("success", true)]
    [InlineData("fault", false)]
    [InlineData("fault", true)]
    [InlineData("custom-fault", false)]
    [InlineData("custom-fault", true)]
    [InlineData("unknown-fault", false)]
    [InlineData("unknown-fault", true)]
    [InlineData("malformed", false)]
    [InlineData("malformed", true)]
    [InlineData("send", false)]
    [InlineData("send", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    [InlineData("read-cancel", false)]
    [InlineData("read-cancel", true)]
    [InlineData("dispose", false)]
    [InlineData("dispose", true)]
    [InlineData("early", false)]
    public async Task Streamed_requests_record_one_pair_and_dispose_the_response(string mode, bool aggregate)
    {
        using var cancellation = new CancellationTokenSource();
        ConstructorCountException.Reset();
        string? faultTypeName = mode switch
        {
            "custom-fault" => typeof(ConstructorCountException).FullName,
            "unknown-fault" => "Unloaded.Exceptions.CustomerSpecificFailure",
            _ => null,
        };
        byte[] payload = mode == "malformed" ? "[[0,1,0,false,null]]"u8.ToArray()
            : await Frames(mode is "fault" or "custom-fault" or "unknown-fault", faultTypeName);
        var transport = new StreamTransport(payload, mode, cancellation);
        var client = new TransportInfoCarrierClient(transport, Serializer);
        using var context = new DbContext(new DbContextOptionsBuilder().Options);
        using var meter = new Recording();
        QueryDataRequest request = Request();
        Exception? failure = await Record.ExceptionAsync(async () =>
        {
            if (aggregate) { await client.QueryDataAsync(request, context, cancellation.Token); }
            else
            {
                await foreach (QueryStreamFrame frame in client.QueryDataStreamAsync(request, context, cancellation.Token))
                {
                    if (mode == "early" && frame is QueryRowFrame) { break; }
                    if (frame is QueryFaultFrame fault) { throw InfoCarrierFaultMapper.Rehydrate(fault.Fault); }
                }
            }
        });
        if (mode is "success" or "early") { Assert.Null(failure); }
        else { Assert.NotNull(failure); }
        Assert.Equal(1, transport.Sends);
        Assert.Equal(mode is "send" or "cancel" ? 0 : 1, transport.Disposals);
        Reading count = Assert.Single(meter.For(InfoCarrierMetrics.RoundTripCountInstrumentName));
        Assert.Equal(mode switch { "success" => "success", "early" => "incomplete", "cancel" or "read-cancel" => "cancelled", _ => "error" },
            count.Tags[InfoCarrierMetrics.OutcomeTagName]);
        if (failure is not null) { Assert.Equal(failure.GetType().FullName, count.Tags[InfoCarrierMetrics.ErrorTypeTagName]); }
        else { Assert.DoesNotContain(InfoCarrierMetrics.ErrorTypeTagName, count.Tags.Keys); }
        if (mode == "custom-fault") { Assert.Equal(1, ConstructorCountException.Creations); }
        meter.AssertPairs(1, 0);
    }

    [Fact]
    public async Task Disposing_an_unstarted_enumerator_does_not_count_a_request()
    {
        using var cancellation = new CancellationTokenSource();
        var transport = new StreamTransport(await Frames(false), "success", cancellation);
        var client = new TransportInfoCarrierClient(transport, Serializer);
        using var context = new DbContext(new DbContextOptionsBuilder().Options);
        using var meter = new Recording();
        await using (IAsyncEnumerator<QueryStreamFrame> frames = client.QueryDataStreamAsync(
            Request(), context).GetAsyncEnumerator()) { }
        Assert.Empty(meter.Values);
        Assert.Equal(0, transport.Sends);
        Assert.Equal(0, transport.Disposals);
    }

    private static void AssertLabel(Reading[] values, string nameTag, string registrationTag, string name, int registration)
        => Assert.Single(values, value => Equals(value.Tags.GetValueOrDefault(nameTag), name)
            && Equals(value.Tags.GetValueOrDefault(registrationTag), registration));

    private static QueryDataRequest Request() => new()
    {
        SerializedQuery = [], TrackingBehavior = QueryTrackingBehavior.NoTracking,
        IsAsync = true, ReturnsSingleResult = false,
    };

    private static async Task<byte[]> Frames(bool fault, string? faultTypeName = null)
    {
        using var destination = new MemoryStream();
        await QueryStreamFrameCodec.WriteAsync(destination, Sequence());
        return destination.ToArray();

        async IAsyncEnumerable<QueryStreamFrame> Sequence()
        {
            yield return new QueryStartFrame(QueryStreamFrameCodec.Version, 0, false, typeof(int).FullName);
            yield return new QueryRowFrame(new DynamicValueNode
            {
                Type = new TypeNode { Name = typeof(int).FullName! }, PrimitiveValue = 42,
            });
            if (fault)
            {
                yield return new QueryFaultFrame(new InfoCarrierFault
                {
                    TypeName = faultTypeName ?? typeof(InvalidOperationException).FullName!,
                    Message = "private",
                });
            }
            else { yield return new QueryCompleteFrame(); }
            await Task.CompletedTask;
        }
    }

    private sealed class StreamTransport(byte[] payload, string mode, CancellationTokenSource cancellation)
        : IInfoCarrierTransport, IInfoCarrierStreamTransport
    {
        internal int Sends { get; private set; }
        internal int Disposals { get; private set; }
        private bool DisposeFails => mode == "dispose";
        public InfoCarrierEnvelope Send(InfoCarrierEnvelope request) => throw new NotSupportedException();
        public Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public ValueTask<IInfoCarrierResponse> SendAsync(InfoCarrierOperation operation,
            Func<Stream, CancellationToken, ValueTask> writeRequest, CancellationToken cancellationToken = default)
        {
            Sends++;
            if (mode == "send") { throw new IOException("private"); }
            if (mode == "cancel")
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellationToken);
            }
            Stream content = mode == "read-cancel"
                ? new CancelAfterPrefixStream(payload[..(Array.LastIndexOf(payload, (byte)'[') - 1)], cancellation)
                : new MemoryStream(payload);
            return ValueTask.FromResult<IInfoCarrierResponse>(new Response(this, content));
        }

        private sealed class Response(StreamTransport owner, Stream content) : IInfoCarrierResponse
        {
            public Stream Content => content;
            public ValueTask DisposeAsync()
            {
                owner.Disposals++;
                content.Dispose();
                if (owner.DisposeFails) { throw new IOException("private"); }
                return ValueTask.CompletedTask;
            }
        }
    }

    // End the source only after the decoder has read a real start/row prefix. Cancellation must
    // reach the next read; returning EOF would test malformed input instead of cancellation.
    private sealed class CancelAfterPrefixStream(byte[] prefix, CancellationTokenSource cancellation) : MemoryStream(prefix)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, cancellationToken);
            if (count == 0)
            {
                await cancellation.CancelAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return count;
        }
    }

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
    }

    public sealed class ConstructorCountException : Exception
    {
        private static int _creations;
        internal static int Creations => Volatile.Read(ref _creations);
        internal static void Reset() => Interlocked.Exchange(ref _creations, 0);
        public ConstructorCountException(string message, Exception? inner) : base(message, inner)
            => Interlocked.Increment(ref _creations);
    }
    public sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    internal sealed record Reading(string Instrument, double Value, Dictionary<string, object?> Tags);
    internal sealed class Recording : IDisposable
    {
        private readonly object _gate = new();
        private bool _disposed;
        private readonly MeterListener _listener = new();
        private readonly ConcurrentQueue<Reading> _values = new();
        private readonly SemaphoreSlim _changed = new(0);
        internal Reading[] Values => _values.ToArray();
        internal Recording()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == InfoCarrierMetrics.MeterName) { listener.EnableMeasurementEvents(instrument); }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.Start();
        }
        private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var tag in tags) { copy.Add(tag.Key, tag.Value); }
            lock (_gate)
            {
                if (_disposed) { return; }
                _values.Enqueue(new Reading(instrument.Name, value, copy));
                _changed.Release();
            }
        }
        internal Reading[] For(string instrument) => Values.Where(value => value.Instrument == instrument).ToArray();
        internal void Clear() => _values.Clear();
        internal async Task WaitForPairs(int clients, int servers)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (For(InfoCarrierMetrics.RoundTripDurationInstrumentName).Length < clients
                || For(InfoCarrierMetrics.ServerRequestDurationInstrumentName).Length < servers)
            {
                await _changed.WaitAsync(deadline.Token);
            }
        }
        internal void AssertPairs(int clients, int servers)
        {
            Assert.Equal(clients, For(InfoCarrierMetrics.RoundTripCountInstrumentName).Length);
            Assert.Equal(clients, For(InfoCarrierMetrics.RoundTripDurationInstrumentName).Length);
            Assert.Equal(servers, For(InfoCarrierMetrics.ServerRequestCountInstrumentName).Length);
            Assert.Equal(servers, For(InfoCarrierMetrics.ServerRequestDurationInstrumentName).Length);
            Assert.All(Values, value => Assert.True(value.Value >= 0));
            foreach (Reading count in Values.Where(value => value.Instrument is
                InfoCarrierMetrics.RoundTripCountInstrumentName or InfoCarrierMetrics.ServerRequestCountInstrumentName))
            {
                string instrument = count.Instrument == InfoCarrierMetrics.RoundTripCountInstrumentName
                    ? InfoCarrierMetrics.RoundTripDurationInstrumentName : InfoCarrierMetrics.ServerRequestDurationInstrumentName;
                Assert.Equal(1, count.Value);
                Assert.Single(For(instrument), duration => count.Tags.Count == duration.Tags.Count
                    && count.Tags.All(tag => Equals(duration.Tags.GetValueOrDefault(tag.Key), tag.Value)));
            }
        }
        public void Dispose()
        {
            _listener.Dispose();
            lock (_gate)
            {
                _disposed = true;
                _changed.Dispose();
            }
        }
    }
}
