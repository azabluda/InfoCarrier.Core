// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

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
public sealed class HttpInfoCarrierTransport(
    HttpClient httpClient,
    IInfoCarrierSerializer serializer,
    string requestUri = "infocarrier") : IInfoCarrierTransport, IInfoCarrierStreamTransport
{
    private static readonly HttpRequestOptionsKey<bool> BrowserResponseStreaming
        = new("WebAssemblyEnableStreamingResponse");
    private readonly HttpClient _httpClient = httpClient;
    private readonly IInfoCarrierSerializer _serializer = serializer;
    private readonly string _requestUri = requestUri;

    /// <summary>The media type used for envelope and query request bodies.</summary>
    public string MediaType { get; init; } = "application/json";

    /// <inheritdoc />
    public InfoCarrierEnvelope Send(InfoCarrierEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);

        byte[] body = _serializer.Serialize(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, _requestUri)
        {
            Content = new ByteArrayContent(body),
        };
        message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(MediaType);

        using HttpResponseMessage response = _httpClient.Send(
            message, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
        byte[] responseBody = ReadContent(response.Content);

        if (!response.IsSuccessStatusCode)
        {
            string detail = System.Text.Encoding.UTF8.GetString(responseBody);
            throw new InfoCarrierTransportException(
                $"The InfoCarrier server at '{_requestUri}' answered {(int)response.StatusCode} "
                + $"({response.ReasonPhrase}). Body: {detail}");
        }

        InfoCarrierEnvelope? envelope;
        try
        {
            envelope = _serializer.Deserialize<InfoCarrierEnvelope>(responseBody);
        }
        catch (Exception ex)
        {
            throw InvalidEnvelope(responseBody.Length, ex);
        }

        return envelope ?? throw InvalidEnvelope(responseBody.Length);
    }

    /// <inheritdoc />
    async ValueTask<IInfoCarrierResponse> IInfoCarrierStreamTransport.SendAsync(
        InfoCarrierOperation operation,
        Func<Stream, CancellationToken, ValueTask> writeRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writeRequest);

        using var request = new HttpRequestMessage(HttpMethod.Post, _requestUri)
        {
            Content = new StreamingContent(writeRequest),
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(MediaType);
        request.Headers.Add("InfoCarrier-Operation", operation.ToString());
        request.Options.Set(BrowserResponseStreaming, true);

        HttpResponseMessage response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                string detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InfoCarrierTransportException(
                    $"The InfoCarrier server at '{_requestUri}' answered {(int)response.StatusCode} "
                    + $"({response.ReasonPhrase}). Body: {detail}");
            }
            finally
            {
                response.Dispose();
            }
        }

        try
        {
            Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new HttpInfoCarrierResponse(response, content);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Posts one envelope and returns the one that comes back.
    /// </summary>
    /// <param name="request">The envelope to send.</param>
    /// <param name="cancellationToken">Cancels the round trip.</param>
    /// <returns>The server's envelope, including a fault it chose to report as data (W5).</returns>
    /// <exception cref="InfoCarrierTransportException">
    ///     The request never reached a server, or what came back was not an envelope. A failure
    ///     the server reported is not this: it arrives inside the envelope.
    /// </exception>
    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        byte[] body = await _serializer.SerializeAsync(request, cancellationToken).ConfigureAwait(false);

        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(MediaType);

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

        InfoCarrierEnvelope? envelope;
        try
        {
            envelope = await _serializer.DeserializeAsync<InfoCarrierEnvelope>(responseBody, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A malformed body -- e.g. a misconfigured proxy or captive portal returning HTML on
            // a 200 -- deserializes as a raw JsonException otherwise, which is a lie about where
            // the fault is: this is a transport failure, not something the server reported.
            throw new InfoCarrierTransportException(
                $"The InfoCarrier server at '{_requestUri}' answered 200 with a body that is not an "
                + $"envelope ({responseBody.Length} bytes).",
                ex);
        }

        return envelope
            ?? throw new InfoCarrierTransportException(
                $"The InfoCarrier server at '{_requestUri}' answered 200 with a body that is not an "
                + $"envelope ({responseBody.Length} bytes).");
    }

    private static byte[] ReadContent(HttpContent content)
    {
        using Stream source = content.ReadAsStream();
        using var destination = new MemoryStream();
        source.CopyTo(destination);
        return destination.ToArray();
    }

    private InfoCarrierTransportException InvalidEnvelope(int length, Exception? innerException = null)
    {
        string message =
            $"The InfoCarrier server at '{_requestUri}' answered 200 with a body that is not an "
            + $"envelope ({length} bytes).";
        return innerException is null
            ? new InfoCarrierTransportException(message)
            : new InfoCarrierTransportException(message, innerException);
    }

    private sealed class StreamingContent(
        Func<Stream, CancellationToken, ValueTask> write) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
            => await write(stream, CancellationToken.None).ConfigureAwait(false);

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
            => await write(stream, cancellationToken).ConfigureAwait(false);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class HttpInfoCarrierResponse(
        HttpResponseMessage response,
        Stream content) : IInfoCarrierResponse
    {
        public Stream Content { get; } = content;

        public async ValueTask DisposeAsync()
        {
            await Content.DisposeAsync().ConfigureAwait(false);
            response.Dispose();
        }
    }
}
