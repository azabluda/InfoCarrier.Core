// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Net;
using InfoCarrier.Core.Common;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class HttpInfoCarrierTransportTest
{
    private static readonly IInfoCarrierSerializer Serializer = new SystemTextJsonInfoCarrierSerializer();

    private static InfoCarrierEnvelope AnEnvelope()
        => new()
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = InfoCarrierOperation.BeginTransaction,
            Payload = Serializer.Serialize<object?>(null),
        };

    [Fact]
    public void Synchronous_send_uses_only_the_synchronous_HTTP_pipeline()
    {
        InfoCarrierEnvelope expected = AnEnvelope() with { CorrelationId = "sync" };
        var handler = new SyncOnlyHandler(_ => Respond(expected));
        var transport = new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        InfoCarrierEnvelope actual = transport.Send(AnEnvelope());

        Assert.Equal("sync", actual.CorrelationId);
        Assert.Equal(1, handler.SyncCalls);
        Assert.Equal(0, handler.AsyncCalls);
    }

    [Fact]
    public async Task It_posts_to_the_configured_relative_uri()
    {
        Uri? seen = null;
        var handler = new StubHandler(request =>
        {
            seen = request.RequestUri;
            return Respond(AnEnvelope());
        });

        var transport = new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        await transport.SendAsync(AnEnvelope());

        Assert.Equal("https://example.test/infocarrier", seen?.ToString());
    }

    [Fact]
    public async Task It_round_trips_an_envelope()
    {
        InfoCarrierEnvelope expected = AnEnvelope() with { CorrelationId = "abc-123" };
        var handler = new StubHandler(_ => Respond(expected));

        var transport = new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        InfoCarrierEnvelope actual = await transport.SendAsync(AnEnvelope());

        Assert.Equal("abc-123", actual.CorrelationId);
        Assert.Equal(InfoCarrierOperation.BeginTransaction, actual.Operation);
    }

    [Fact]
    public async Task A_non_success_status_is_reported_with_the_status_and_the_body()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream is down"),
        });

        var transport = new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        InfoCarrierTransportException exception =
            await Assert.ThrowsAsync<InfoCarrierTransportException>(() => transport.SendAsync(AnEnvelope()));

        Assert.Contains("502", exception.Message);
        Assert.Contains("upstream is down", exception.Message);
    }

    [Fact]
    public async Task A_200_body_that_is_not_an_envelope_is_reported_as_a_transport_failure()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>not json</html>"),
        });

        var transport = new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        InfoCarrierTransportException exception =
            await Assert.ThrowsAsync<InfoCarrierTransportException>(() => transport.SendAsync(AnEnvelope()));

        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task Stream_exchange_writes_request_and_returns_response_without_buffering()
    {
        byte[] expected = [1, 2, 3];
        byte[]? requestBody = null;
        bool streamingEnabled = false;
        var handler = new AsyncStubHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            streamingEnabled = request.Options.TryGetValue(
                new HttpRequestOptionsKey<bool>("WebAssemblyEnableStreamingResponse"), out bool enabled)
                && enabled;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expected),
            };
        });

        var transport = (IInfoCarrierStreamTransport)new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        await using IInfoCarrierResponse response = await transport.SendAsync(
            InfoCarrierOperation.Query,
            async (destination, cancellationToken) =>
                await destination.WriteAsync(expected, cancellationToken));

        Assert.Equal(expected, requestBody);
        Assert.True(streamingEnabled);
        Assert.Equal(expected, await ReadAllAsync(response.Content));
    }

    [Fact]
    public async Task Disposing_stream_response_disposes_its_content()
    {
        var content = new TrackingContent();
        var handler = new AsyncStubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        var transport = (IInfoCarrierStreamTransport)new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        IInfoCarrierResponse response = await transport.SendAsync(
            InfoCarrierOperation.Query, (_, _) => ValueTask.CompletedTask);
        await response.DisposeAsync();

        Assert.True(content.Disposed);
    }

    private static HttpResponseMessage Respond(InfoCarrierEnvelope envelope)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Serializer.Serialize(envelope)) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private sealed class SyncOnlyHandler(
        Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int SyncCalls { get; private set; }
        public int AsyncCalls { get; private set; }

        protected override HttpResponseMessage Send(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SyncCalls++;
            return respond(request);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AsyncCalls++;
            throw new InvalidOperationException("The asynchronous HTTP path was called.");
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream source)
    {
        using var destination = new MemoryStream();
        await source.CopyToAsync(destination);
        return destination.ToArray();
    }

    private sealed class AsyncStubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request, cancellationToken);
    }

    private sealed class TrackingContent : ByteArrayContent
    {
        public TrackingContent()
            : base([])
        {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = disposing;
            base.Dispose(disposing);
        }
    }
}
