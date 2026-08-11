using System.Net.Http.Headers;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;

namespace Northwind.Client.Transport;

/// <summary>
///     Carries an <see cref="InfoCarrierEnvelope" /> to a server over HTTP.
/// </summary>
/// <remarks>
///     <para>
///         The whole transport seam is one method, so this is the whole transport. The server
///         half is the <c>MapInfoCarrier</c> endpoint, which hands the envelope to the product's
///         existing <c>InfoCarrierEnvelopeServer</c>.
///     </para>
///     <para>
///         <b>Deliberately free of sample types</b>, so that promoting it into an
///         <c>InfoCarrier.Core.Http</c> package is a file move (spec 4.1). Nothing here references
///         Northwind, and nothing here needs ASP.NET.
///     </para>
/// </remarks>
public sealed class HttpInfoCarrierTransport : IInfoCarrierTransport
{
    private readonly HttpClient _httpClient;
    private readonly IInfoCarrierSerializer _serializer;
    private readonly string _requestUri;

    public HttpInfoCarrierTransport(
        HttpClient httpClient,
        IInfoCarrierSerializer serializer,
        string requestUri = "infocarrier")
    {
        _httpClient = httpClient;
        _serializer = serializer;
        _requestUri = requestUri;
    }

    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        byte[] body = await _serializer.SerializeAsync(request, cancellationToken).ConfigureAwait(false);

        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response =
            await _httpClient.PostAsync(_requestUri, content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Both the status and the body. A transport failure reported as a bare status code
            // is indistinguishable from a dozen unrelated causes to whoever has to diagnose it.
            string detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InfoCarrierTransportException(
                $"The InfoCarrier server at '{_requestUri}' answered {(int)response.StatusCode} "
                + $"({response.ReasonPhrase}). Body: {detail}");
        }

        byte[] responseBody =
            await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        return await _serializer.DeserializeAsync<InfoCarrierEnvelope>(responseBody, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InfoCarrierTransportException(
                $"The InfoCarrier server at '{_requestUri}' answered 200 with a body that is not an "
                + $"envelope ({responseBody.Length} bytes).");
    }
}
