// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Net;
using System.Reflection;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class ProtocolCompatibilityTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    // Fixed old contract inputs, not regenerated output baselines. An incompatible request must
    // be refused even when its payload could otherwise reach an operation server.
    private const string OldEnvelope = """{"protocolVersion":2,"operation":2,"payload":"bnVsbA=="}""";
    private const string OldStreamRequest = """{"serializedQuery":"bnVsbA==","trackingBehavior":0,"isAsync":true,"returnsSingleResult":false}""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pre_policy_payloads_are_refused_without_invoking_the_operation_server(bool streamed)
    {
        IInfoCarrierServer server = DispatchProxy.Create<IInfoCarrierServer, CountingServer>();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton(server)));
        using HttpClient client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "infocarrier")
        {
            Content = new StringContent(streamed ? OldStreamRequest : OldEnvelope,
                System.Text.Encoding.UTF8, "application/json"),
        };
        if (streamed) { request.Headers.Add("InfoCarrier-Operation", "Query"); }

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(InfoCarrierEnvelope.CurrentProtocolVersion.ToString(),
            Assert.Single(response.Headers.GetValues("InfoCarrier-Protocol-Version")));
        string detail = await response.Content.ReadAsStringAsync();
        Assert.Contains($"client {(streamed ? 0 : 2)}", detail);
        Assert.Contains($"server {InfoCarrierEnvelope.CurrentProtocolVersion}", detail);
        Assert.Contains("Upgrade the client", detail);
        Assert.Equal(0, ((CountingServer)server).Calls);
    }

    [Fact]
    public async Task Missing_envelope_version_is_a_compatibility_refusal()
    {
        using HttpClient client = factory.CreateClient();
        using var request = new StringContent("""{"operation":2,"payload":"bnVsbA=="}""",
            System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.PostAsync("infocarrier", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("client 0", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.Contains("InfoCarrier-Protocol-Version"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Stream_identification_is_checked_before_deserializing_an_unknown_body(int version)
    {
        IInfoCarrierServer server = DispatchProxy.Create<IInfoCarrierServer, CountingServer>();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton(server)));
        using HttpClient client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "infocarrier")
        {
            Content = new StringContent("not the current query format"),
        };
        request.Headers.Add("InfoCarrier-Stream-Operation", "Query");
        if (version != 0) { request.Headers.Add("InfoCarrier-Protocol-Version", version.ToString()); }

        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(version.ToString(), Assert.Single(response.Headers.GetValues("InfoCarrier-Client-Protocol-Version")));
        Assert.Contains($"client {version}", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, ((CountingServer)server).Calls);
    }

    [Fact]
    public async Task Stream_transport_preserves_the_versions_reported_in_a_refusal()
    {
        var transport = (IInfoCarrierStreamTransport)new HttpInfoCarrierTransport(
            factory.CreateClient(), new SystemTextJsonInfoCarrierSerializer());
        InfoCarrierProtocolException exception = await Assert.ThrowsAsync<InfoCarrierProtocolException>(async () =>
        {
            await using IInfoCarrierResponse response = await transport.SendAsync(InfoCarrierOperation.Query,
                async (stream, token) => await stream.WriteAsync(
                    System.Text.Encoding.UTF8.GetBytes(OldStreamRequest), token));
        });
        Assert.Equal(0, exception.ClientVersion);
        Assert.Equal(InfoCarrierEnvelope.CurrentProtocolVersion, exception.ServerVersion);
        Assert.Contains("Upgrade the client", exception.Message);
    }

    [Fact]
    public async Task New_stream_requests_do_not_select_a_legacy_servers_unversioned_execution_path()
    {
        var handler = new LegacySelectorHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var transport = (IInfoCarrierStreamTransport)new HttpInfoCarrierTransport(http, new SystemTextJsonInfoCarrierSerializer());
        await Assert.ThrowsAsync<InfoCarrierTransportException>(async () =>
        {
            await using IInfoCarrierResponse response = await transport.SendAsync(InfoCarrierOperation.Query,
                async (stream, token) => await stream.WriteAsync(
                    System.Text.Encoding.UTF8.GetBytes(OldStreamRequest), token));
        });
        Assert.Equal(0, handler.Executions);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Client_checks_response_version_before_fault_or_payload(bool synchronous, bool newerServer)
    {
        int remote = InfoCarrierEnvelope.CurrentProtocolVersion + (newerServer ? 1 : -1);
        var response = new InfoCarrierEnvelope
        {
            ProtocolVersion = remote,
            Operation = InfoCarrierOperation.BeginTransaction,
            Payload = [],
            Fault = new InfoCarrierFault { TypeName = typeof(InvalidOperationException).FullName!, Message = "untrusted fault" },
        };
        var client = new TransportInfoCarrierClient(new ResponseTransport(response), new SystemTextJsonInfoCarrierSerializer());

        InfoCarrierProtocolException exception = synchronous
            ? Assert.Throws<InfoCarrierProtocolException>(() => client.BeginTransaction())
            : await Assert.ThrowsAsync<InfoCarrierProtocolException>(() => client.BeginTransactionAsync());

        Assert.Equal(InfoCarrierEnvelope.CurrentProtocolVersion, exception.ClientVersion);
        Assert.Equal(remote, exception.ServerVersion);
        Assert.Contains(newerServer ? "Upgrade the client" : "Upgrade the server", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HTTP_refusal_is_a_permanent_protocol_exception(bool synchronous)
    {
        using var http = new HttpClient(new RefusalHandler()) { BaseAddress = new Uri("https://example.test/") };
        var transport = new HttpInfoCarrierTransport(http, new SystemTextJsonInfoCarrierSerializer());
        var request = new InfoCarrierEnvelope
        {
            ProtocolVersion = 2,
            Operation = InfoCarrierOperation.BeginTransaction,
            Payload = "null"u8.ToArray(),
        };
        InfoCarrierProtocolException exception = synchronous
            ? Assert.Throws<InfoCarrierProtocolException>(() => transport.Send(request))
            : await Assert.ThrowsAsync<InfoCarrierProtocolException>(() => transport.SendAsync(request));
        Assert.Equal(2, exception.ClientVersion);
        Assert.Equal(InfoCarrierEnvelope.CurrentProtocolVersion, exception.ServerVersion);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Stream_response_version_is_checked_before_rows(int version)
    {
        await using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"[[0,{version},0,false,null],[3]]"));
        InfoCarrierProtocolException exception = await Assert.ThrowsAsync<InfoCarrierProtocolException>(
            async () => await QueryStreamFrameCodec.ReadAsync(source).ToListAsync());
        Assert.Equal(version, exception.ServerVersion);
    }

    private sealed class ResponseTransport(InfoCarrierEnvelope response) : IInfoCarrierTransport
    {
        public InfoCarrierEnvelope Send(InfoCarrierEnvelope request) => response;
        public Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => Task.FromResult(response);
    }

    private sealed class RefusalHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("version refusal") };
            response.Headers.Add("InfoCarrier-Protocol-Version", InfoCarrierEnvelope.CurrentProtocolVersion.ToString());
            return response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }

    // Only the pre-policy routing rule is reproduced here, not a compatibility implementation.
    // Without its old selector that endpoint attempts to read an envelope, whose required operation
    // and payload are absent from a streamed query body. Refusal needs no operation execution.
    private sealed class LegacySelectorHandler : HttpMessageHandler
    {
        public int Executions { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Contains("InfoCarrier-Operation")) { Executions++; }
            Assert.Equal("Query", Assert.Single(request.Headers.GetValues("InfoCarrier-Stream-Operation")));
            byte[] body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            Assert.Throws<System.Text.Json.JsonException>(() => new SystemTextJsonInfoCarrierSerializer().Deserialize<InfoCarrierEnvelope>(body));
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Not an envelope.") };
        }
    }

    public class CountingServer : DispatchProxy
    {
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls++;
            throw new InvalidOperationException("An incompatible request reached execution.");
        }
    }
}
