// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Net.Http.Headers;
using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core;

/// <summary>
///     Carries an <see cref="InfoCarrierEnvelope" /> to a server over HTTP.
/// </summary>
/// <remarks>
///     <para>
///         Two legs, because the wire has two shapes. Eight operations answer with one envelope,
///         which <see cref="SendAsync" /> buffers and deserializes. A query answers with a stream
///         of <see cref="QueryStreamItem" />, which <see cref="SendQueryAsync" /> reads as it
///         arrives (<c>docs/architecture.md</c> §6a <b>D7</b>).
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
    string requestUri = "infocarrier") : IInfoCarrierTransport
{
    /// <summary>
    ///     Asks a Blazor WebAssembly host not to buffer the whole response before handing any of
    ///     it over.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The browser buffers by default, and without this the streaming leg streams
    ///         nothing.</b> The rows would arrive exactly as fast as the last of them, which is the
    ///         buffered behaviour D7 exists to remove — and it would look as though the feature
    ///         does not work rather than as though it is switched off. This repository has been
    ///         caught by a WebAssembly default twice already (blocking lazy loads, the compiled
    ///         model's 10 MB-stack thread), which is why the browser proof is executed rather than
    ///         assumed.
    ///     </para>
    ///     <para>
    ///         <b>Set through the option key rather than through
    ///         <c>SetBrowserResponseStreamingEnabled</c>, and that is forced.</b> That extension
    ///         method ships in <c>Microsoft.AspNetCore.Components.WebAssembly</c>, and this assembly
    ///         targets <c>net10.0</c> with no Blazor dependency so that one package serves WPF,
    ///         MAUI, console and WebAssembly clients alike.
    ///     </para>
    ///     <para>
    ///         <b>The literal is verified against both ends rather than remembered</b>, because a
    ///         wrong option key is silent — <c>HttpRequestOptions</c> accepts any key, so a typo
    ///         would leave the browser buffering with nothing to show for it.
    ///         <c>WebAssemblyEnableStreamingResponse</c> is present in the user-string heap of
    ///         <b>both</b> <c>Microsoft.AspNetCore.Components.WebAssembly.dll</c> (which writes it,
    ///         from <c>SetBrowserResponseStreamingEnabled</c>) and the browser build of
    ///         <c>System.Net.Http.dll</c> (whose <c>BrowserHttpHandler</c> reads it), at 10.0.11.
    ///         Do not confuse it with <c>System.Net.Http.WasmEnableStreamingResponse</c>, which is
    ///         in the same assembly and is the <em>global</em> AppContext switch behind
    ///         <c>DOTNET_WASM_ENABLE_STREAMING_RESPONSE</c>, not the per-request option.
    ///     </para>
    ///     <para>
    ///         Every non-browser handler ignores an option it does not know, so this costs a
    ///         desktop or server client nothing.
    ///     </para>
    /// </remarks>
    private static readonly HttpRequestOptionsKey<bool> BrowserResponseStreaming
        = new("WebAssemblyEnableStreamingResponse");

    private readonly HttpClient _httpClient = httpClient;
    private readonly IInfoCarrierSerializer _serializer = serializer;
    private readonly string _requestUri = requestUri;

    /// <inheritdoc />
    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using HttpResponseMessage response = await PostAsync(
            request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

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

    /// <inheritdoc />
    public async Task<QueryDataResult> SendQueryAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ResponseHeadersRead, which is the whole of the client half of D7: the default,
        // ResponseContentRead, completes the task only once the last byte of the body has arrived,
        // so every row would still be in memory before the first one was decoded.
        //
        // `StreamingOverHttpTest` is the pin on this one word, and it was verified by putting
        // `ResponseContentRead` back: the test fails on its deadline.
        HttpResponseMessage response = await PostAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        try
        {
            Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            IAsyncEnumerable<QueryStreamItem> items = JsonSerializer.DeserializeAsyncEnumerable(
                Bounded(body),
                ExpressionJsonContext.Default.QueryStreamItem!,
                cancellationToken)!;

            // The response is handed over, not disposed here: it has to outlive this method for
            // exactly as long as the rows are being read, and the reader closes it.
            return await QueryStreamReader
                .ReadAsync(items, _requestUri, response, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     The response body, with <see cref="InfoCarrierPayloadLimits.MaxResponseBytes" /> applied
    ///     as it is read.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Streaming is what makes this bound necessary.</b> While the server buffered, a
    ///         ruinous result was at least ruinous locally and visible; now an unbounded response is
    ///         the ordinary path, and nothing between the query and the client's memory counts it.
    ///         The bound could not be applied to a stream by the old
    ///         <c>Guard(payload.Length, …)</c> shape, which needs the bytes already in hand — so it
    ///         is counted as it goes and refused at the point it is passed.
    ///     </para>
    ///     <para>
    ///         <b>The default is still <see langword="null" />, and that is unchanged</b>: a result
    ///         travelling back is something the client asked its own server for, and this library
    ///         has no basis for capping it (see <see cref="InfoCarrierPayloadLimits" />, and the
    ///         560 MB result C37 measured in this repository's own suite). What changes is that the
    ///         setting now means something on the path that made it matter.
    ///     </para>
    /// </remarks>
    private Stream Bounded(Stream body)
        => _serializer.Limits.MaxResponseBytes is { } max
            ? new BoundedReadStream(body, max, _requestUri)
            : body;

    private async Task<HttpResponseMessage> PostAsync(
        InfoCarrierEnvelope request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        byte[] body = await _serializer.SerializeAsync(request, cancellationToken).ConfigureAwait(false);

        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var message = new HttpRequestMessage(HttpMethod.Post, _requestUri) { Content = content };
        message.Options.Set(BrowserResponseStreaming, true);

        HttpResponseMessage response = await _httpClient
            .SendAsync(message, completionOption, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        try
        {
            // Both the status and the body. A transport failure reported as a bare status code
            // is indistinguishable from a dozen unrelated causes to whoever has to diagnose it.
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

    /// <summary>
    ///     A read-only pass-through that refuses to hand over more than a fixed number of bytes.
    /// </summary>
    /// <remarks>
    ///     Only the two read paths are overridden, because only they can grow the total; everything
    ///     else delegates. The refusal names both numbers and the setting, exactly as
    ///     <see cref="InfoCarrierPayloadLimits" />'s own guard does, because a refusal naming
    ///     neither is indistinguishable from a corrupt payload to whoever has to raise the limit.
    /// </remarks>
    private sealed class BoundedReadStream(Stream inner, int max, string origin) : Stream
    {
        private long _read;

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => Counted(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Counted(inner.Read(buffer));

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
            => Counted(await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));

        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Counted(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false));

        public override void Flush() => inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync() => inner.DisposeAsync();

        private int Counted(int count)
        {
            _read += count;

            return _read <= max
                ? count
                : throw new InvalidOperationException(
                    $"The query response from '{origin}' has passed {_read} bytes, which exceeds the "
                    + $"maximum of {max} bytes (InfoCarrierPayloadLimits.MaxResponseBytes). Raise the "
                    + "limit on the configured serializer, or pass null to opt out of it.");
        }
    }
}
