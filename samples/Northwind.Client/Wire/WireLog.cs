// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace Northwind.Client.Wire;

/// <summary>
///     The last <see cref="Capacity" /> round trips, newest first.
/// </summary>
/// <remarks>
///     A fixed ring rather than a growing list, so a long session cannot grow the browser's memory
///     without bound (spec §5.2). <see cref="Sequence" /> keeps counting past the window, so the
///     panel can still say which round trip a row is.
/// </remarks>
public sealed class WireLog
{
    /// <summary>How many entries the panel keeps.</summary>
    public const int Capacity = 20;

    private readonly List<WireEntry> _entries = [];
    private int _sequence;

    /// <summary>
    ///     Raised after every change. The panel subscribes; nothing else should need to.
    /// </summary>
    public event Action? Changed;

    /// <summary>The entries, newest first.</summary>
    public IReadOnlyList<WireEntry> Entries => _entries;

    /// <summary>How many round trips have happened in total, including ones aged out of the ring.</summary>
    public int Sequence => _sequence;

    /// <summary>Records one completed round trip.</summary>
    public void Record(
        InfoCarrierEnvelope request,
        InfoCarrierEnvelope response,
        int requestBytes,
        int responseBytes,
        TimeSpan duration)
        => Add(new WireEntry
        {
            Sequence = ++_sequence,
            Operation = request.Operation,
            RequestBytes = requestBytes,
            ResponseBytes = responseBytes,
            Duration = duration,
            RequestPayload = WireDecoder.Describe(request.Payload),
            ResponsePayload = WireDecoder.Describe(response.Payload),

            // A server-side failure arrives as data rather than as an exception (W5), so a
            // faulted round trip is a perfectly ordinary one from the transport's point of view.
            // The panel is the only place it is visible before EF re-raises it.
            Fault = response.Fault is { } fault ? $"{fault.TypeName}: {fault.Message}" : null,
        });

    /// <summary>
    ///     Records a round trip the transport could not complete — no response envelope exists.
    /// </summary>
    public void RecordFailure(InfoCarrierEnvelope request, int requestBytes, TimeSpan duration, Exception exception)
        => Add(new WireEntry
        {
            Sequence = ++_sequence,
            Operation = request.Operation,
            RequestBytes = requestBytes,
            ResponseBytes = 0,
            Duration = duration,
            RequestPayload = WireDecoder.Describe(request.Payload),
            ResponsePayload = string.Empty,
            Fault = $"{exception.GetType().Name}: {exception.Message}",
        });

    /// <summary>Empties the panel. Does not reset <see cref="Sequence" />.</summary>
    public void Clear()
    {
        _entries.Clear();
        Changed?.Invoke();
    }

    private void Add(WireEntry entry)
    {
        _entries.Insert(0, entry);

        if (_entries.Count > Capacity)
        {
            _entries.RemoveRange(Capacity, _entries.Count - Capacity);
        }

        Changed?.Invoke();
    }
}
