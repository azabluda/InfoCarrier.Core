// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Net;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;
using InfoCarrier.Core.Wire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Old = InfoCarrier.Core.Wire.V3.Common;
using OldNodes = InfoCarrier.Core.Wire.V3.Expressions;

namespace InfoCarrier.Core.TransportTests;

public class ProtocolCodecServicesTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removing_a_codec_refuses_its_requests_before_operation_execution(bool streamed)
    {
        IInfoCarrierServer server = DispatchProxy.Create<IInfoCarrierServer, ProtocolCompatibilityTest.CountingServer>();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWireProtocolCodec>();
            services.AddSingleton<IWireProtocolCodec, CurrentWireProtocolCodec>();
            services.AddSingleton(server);
        }));
        using HttpClient http = host.CreateClient();
        byte[] body = streamed ? "invalid graph"u8.ToArray() : JsonSerializer.SerializeToUtf8Bytes(new Old.InfoCarrierEnvelope
        {
            ProtocolVersion = 3, Operation = Old.InfoCarrierOperation.Query, Payload = [],
        }, Wire.V3.Protocol3JsonContext.Default.InfoCarrierEnvelope);
        using var request = new HttpRequestMessage(HttpMethod.Post, "infocarrier") { Content = new ByteArrayContent(body) };
        if (streamed)
        {
            request.Headers.Add("InfoCarrier-Stream-Operation", "Query");
            request.Headers.Add("InfoCarrier-Protocol-Version", "3");
        }
        using HttpResponseMessage response = await http.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("3", Assert.Single(response.Headers.GetValues("InfoCarrier-Client-Protocol-Version")));
        Assert.Equal(0, ((ProtocolCompatibilityTest.CountingServer)server).Calls);
    }

    [Fact]
    public void Repeated_registration_is_idempotent_and_duplicate_versions_are_rejected()
    {
        var services = new ServiceCollection();
        services.AddInfoCarrierWireProtocols().AddInfoCarrierWireProtocols();
        using (ServiceProvider provider = services.BuildServiceProvider())
        {
            Assert.Equal(new[] { 3, 4 }, provider.GetServices<IWireProtocolCodec>().Select(codec => codec.Version).Order());
        }
        services.AddSingleton<IWireProtocolCodec, CurrentWireProtocolCodec>();
        using ServiceProvider duplicate = services.BuildServiceProvider();
        Assert.Contains("exactly one", Assert.Throws<InvalidOperationException>(
            duplicate.GetRequiredService<InfoCarrierWireProtocol>).Message);
        Assert.Contains("current wire protocol", Assert.Throws<InvalidOperationException>(
            () => new InfoCarrierWireProtocol([new Protocol3WireProtocolCodec()])).Message);
    }

    [Fact]
    public void Codec_metadata_is_isolated_between_service_providers()
    {
        var custom = new ServiceCollection();
        custom.AddInfoCarrierWireProtocols();
        custom.AddSingleton<IWireProtocolCodec, MarkerCodec>();
        using ServiceProvider customProvider = custom.BuildServiceProvider();
        var customSerializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default,
            customProvider.GetRequiredService<InfoCarrierWireProtocol>());
        var marker = new ProtocolMarker(7);
        Assert.Equal(marker, customSerializer.Deserialize<ProtocolMarker>(customSerializer.Serialize(marker)));

        var standard = new ServiceCollection();
        standard.AddInfoCarrierWireProtocols();
        using ServiceProvider standardProvider = standard.BuildServiceProvider();
        InfoCarrierWireProtocol standardProtocol = standardProvider.GetRequiredService<InfoCarrierWireProtocol>();
        Assert.False(standardProtocol.IsSupported(97));
        var standardSerializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, standardProtocol);
        Assert.Throws<NotSupportedException>(() => standardSerializer.Serialize(marker));
    }

    [Fact]
    public async Task Hosted_buffered_streamed_and_custom_adapters_use_the_injected_codec()
    {
        var calls = new CodecCalls();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWireProtocolCodec>();
            services.AddSingleton<IWireProtocolCodec, CurrentWireProtocolCodec>();
            services.AddSingleton(calls);
            services.AddSingleton<IWireProtocolCodec, TrackingProtocol3Codec>();
        }));
        using HttpClient http = host.CreateClient();
        IInfoCarrierSerializer serializer = host.Services.GetRequiredService<IInfoCarrierSerializer>();
        var invalid = new Old.QueryDataRequest
        {
            SerializedQuery = "{\"$kind\":99999}"u8.ToArray(),
            TrackingBehavior = Old.QueryTrackingBehavior.NoTracking, IsAsync = true, ReturnsSingleResult = true,
        };
        byte[] body = serializer.Serialize(new Old.InfoCarrierEnvelope
        {
            ProtocolVersion = 3, Operation = Old.InfoCarrierOperation.Query, Payload = serializer.Serialize(invalid),
        });
        using HttpResponseMessage buffered = await http.PostAsync("infocarrier", new ByteArrayContent(body), TestContext.Current.CancellationToken);
        buffered.EnsureSuccessStatusCode();
        Old.InfoCarrierEnvelope response = serializer.Deserialize<Old.InfoCarrierEnvelope>(await buffered.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))!;
        Assert.Equal(3, response.ProtocolVersion);
        Assert.NotNull(response.Fault);
        Assert.True(calls.Has("envelope") && calls.Has("decode") && calls.Has("encode"));

        var valid = invalid with
        {
            ProtocolVersion = 3,
            SerializedQuery = JsonSerializer.SerializeToUtf8Bytes<OldNodes.ExpressionNode>(new OldNodes.ConstantNode
            {
                Type = new OldNodes.TypeNode { Name = "System.Int32" }, PrimitiveValue = 42,
            }, OldNodes.Protocol3ExpressionJsonContext.Default.ExpressionNode),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "infocarrier") { Content = new ByteArrayContent(serializer.Serialize(valid)) };
        request.Headers.Add("InfoCarrier-Stream-Operation", "Query");
        request.Headers.Add("InfoCarrier-Protocol-Version", "3");
        using HttpResponseMessage streamed = await http.SendAsync(request, TestContext.Current.CancellationToken);
        streamed.EnsureSuccessStatusCode();
        await using Stream source = await streamed.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        List<Wire.V3.Query.QueryStreamFrame> frames = await Wire.V3.Query.QueryStreamFrameCodec.ReadAsync(source, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, Assert.IsType<Wire.V3.Query.QueryStartFrame>(frames[0]).Version);
        Assert.True(calls.Has("query") && calls.Has("frames"));

        int before = calls.Count("decode");
        InfoCarrierEnvelopeServer adapter = host.Services.GetInfoCarrierServerEndpoint().CreateEnvelopeServer();
        InfoCarrierEnvelope adapted = await adapter.DispatchAsync(new InfoCarrierEnvelope
        {
            ProtocolVersion = 3, Operation = InfoCarrierOperation.Query, Payload = serializer.Serialize(invalid),
        }, TestContext.Current.CancellationToken);
        Assert.NotNull(adapted.Fault);
        Assert.Equal(before + 1, calls.Count("decode"));
    }

    private sealed class CodecCalls
    {
        private readonly ConcurrentDictionary<string, int> _calls = new();
        internal void Record(string method) => _calls.AddOrUpdate(method, 1, (_, count) => count + 1);
        internal int Count(string method) => _calls.GetValueOrDefault(method);
        internal bool Has(string method) => Count(method) > 0;
    }

    private sealed class TrackingProtocol3Codec(CodecCalls calls) : IWireProtocolCodec
    {
        private readonly Protocol3WireProtocolCodec _inner = new();
        public int Version => _inner.Version;
        public JsonSerializerContext JsonContext => _inner.JsonContext;
        public bool IsKnownOperation(InfoCarrierOperation operation) => _inner.IsKnownOperation(operation);
        public T? Deserialize<T>(IInfoCarrierSerializer serializer, byte[] payload, InfoCarrierPayloadLimits limits)
        { calls.Record("decode"); return _inner.Deserialize<T>(serializer, payload, limits); }
        public byte[] Serialize<T>(IInfoCarrierSerializer serializer, T value)
        { calls.Record("encode"); return _inner.Serialize(serializer, value); }
        public ValueTask<InfoCarrierEnvelope?> ReadEnvelopeAsync(IInfoCarrierSerializer serializer, Stream source, CancellationToken token)
        { calls.Record("envelope"); return _inner.ReadEnvelopeAsync(serializer, source, token); }
        public ValueTask<QueryDataRequest?> ReadQueryAsync(IInfoCarrierSerializer serializer, Stream source, InfoCarrierPayloadLimits limits, CancellationToken token)
        { calls.Record("query"); return _inner.ReadQueryAsync(serializer, source, limits, token); }
        public ValueTask WriteFramesAsync(Stream destination, IAsyncEnumerable<QueryStreamFrame> frames, CancellationToken token)
        { calls.Record("frames"); return _inner.WriteFramesAsync(destination, frames, token); }
    }

    private sealed class MarkerCodec : IWireProtocolCodec
    {
        public int Version => 97;
        public JsonSerializerContext JsonContext => ProtocolMarkerJsonContext.Default;
        public bool IsKnownOperation(InfoCarrierOperation operation) => false;
        public T? Deserialize<T>(IInfoCarrierSerializer serializer, byte[] payload, InfoCarrierPayloadLimits limits) => throw new NotSupportedException();
        public byte[] Serialize<T>(IInfoCarrierSerializer serializer, T value) => throw new NotSupportedException();
        public ValueTask<InfoCarrierEnvelope?> ReadEnvelopeAsync(IInfoCarrierSerializer serializer, Stream source, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<QueryDataRequest?> ReadQueryAsync(IInfoCarrierSerializer serializer, Stream source, InfoCarrierPayloadLimits limits, CancellationToken token) => throw new NotSupportedException();
        public ValueTask WriteFramesAsync(Stream destination, IAsyncEnumerable<QueryStreamFrame> frames, CancellationToken token) => throw new NotSupportedException();
    }
}

internal sealed record ProtocolMarker(int Value);

[JsonSerializable(typeof(ProtocolMarker))]
internal partial class ProtocolMarkerJsonContext : JsonSerializerContext;
