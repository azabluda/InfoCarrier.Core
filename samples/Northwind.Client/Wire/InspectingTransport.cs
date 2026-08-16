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
}
