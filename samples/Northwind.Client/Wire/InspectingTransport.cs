// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;

namespace Northwind.Client.Wire;

/// <summary>
///     Wraps another <see cref="IInfoCarrierTransport" /> and reports every round trip to the
///     <see cref="WireLog" />.
/// </summary>
/// <remarks>
///     <para>
///         <b>A decorator, not a change to <c>HttpInfoCarrierTransport</c>, and that is a
///         constraint rather than a preference.</b> That file is written to be promoted into an
///         <c>InfoCarrier.Core.Http</c> package by moving it (spec §4.1), which it can only be if
///         it stays free of sample types. Putting the panel behind the one-method seam costs
///         nothing and keeps the promotion a file move.
///     </para>
///     <para>
///         It also demonstrates the seam doing what a seam is for: an application can insert
///         retry, logging, compression or authentication at exactly this point, without the
///         provider knowing.
///     </para>
/// </remarks>
public sealed class InspectingTransport(
    IInfoCarrierTransport inner,
    IInfoCarrierSerializer serializer,
    WireLog log) : IInfoCarrierTransport
{
    private readonly IInfoCarrierTransport _inner = inner;
    private readonly IInfoCarrierSerializer _serializer = serializer;
    private readonly WireLog _log = log;

    /// <inheritdoc />
    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Serialized a second time purely to size it. The inner transport will serialize it
        // again for the send, and that is accepted here: the alternative is to report the
        // payload length and call it the envelope size, which would be a wrong number. The demo
        // dropped a byte counter in Phase 1 rather than print one of those.
        int requestBytes = _serializer.Serialize(request).Length;

        long started = Stopwatch.GetTimestamp();

        InfoCarrierEnvelope response;
        try
        {
            response = await _inner.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _log.RecordFailure(request, requestBytes, Stopwatch.GetElapsedTime(started), exception);
            throw;
        }

        _log.Record(
            request,
            response,
            requestBytes,
            _serializer.Serialize(response).Length,
            Stopwatch.GetElapsedTime(started));

        return response;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         <b>What the panel can honestly report about a streamed response is different, and
    ///         pretending otherwise would be the wrong demonstration.</b> There is no response
    ///         envelope to show and no total byte count to print at the moment the call returns —
    ///         the rows have not arrived yet. What <em>is</em> known then is the one number
    ///         streaming exists to improve: how long until the server said something.
    ///     </para>
    ///     <para>
    ///         The elapsed time recorded here is therefore <b>time to first byte</b>, not time to
    ///         the last row, and the log says so. Buffering the rows in order to report a size
    ///         would undo the feature in the act of displaying it.
    ///     </para>
    /// </remarks>
    public async Task<QueryDataResult> SendQueryAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        int requestBytes = _serializer.Serialize(request).Length;
        long started = Stopwatch.GetTimestamp();

        try
        {
            QueryDataResult result = await _inner.SendQueryAsync(request, cancellationToken).ConfigureAwait(false);
            _log.RecordStreamStart(request, requestBytes, Stopwatch.GetElapsedTime(started));
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _log.RecordFailure(request, requestBytes, Stopwatch.GetElapsedTime(started), exception);
            throw;
        }
    }
}
