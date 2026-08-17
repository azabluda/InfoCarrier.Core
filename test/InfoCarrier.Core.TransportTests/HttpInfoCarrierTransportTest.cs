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

    private static HttpResponseMessage Respond(InfoCarrierEnvelope envelope)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Serializer.Serialize(envelope)) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
