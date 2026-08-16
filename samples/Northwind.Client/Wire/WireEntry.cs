// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace Northwind.Client.Wire;

/// <summary>
///     One round trip, as the inspector panel shows it.
/// </summary>
public sealed record WireEntry
{
    /// <summary>1-based, in the order the client issued them. Never reused.</summary>
    public required int Sequence { get; init; }

    /// <summary>Which of the nine operations this was.</summary>
    public required InfoCarrierOperation Operation { get; init; }

    /// <summary>Size of the serialized request envelope, in bytes.</summary>
    public required int RequestBytes { get; init; }

    /// <summary>Size of the serialized response envelope, in bytes. Zero if the send failed.</summary>
    public required int ResponseBytes { get; init; }

    /// <summary>Wall-clock time from handing the envelope to the transport to getting one back.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>The request payload, decoded for reading. See <see cref="WireDecoder" />.</summary>
    public required string RequestPayload { get; init; }

    /// <summary>The response payload, decoded for reading. Empty if the send failed.</summary>
    public required string ResponsePayload { get; init; }

    /// <summary>
    ///     Set when the server reported a failure as data (wire-protocol W5), or when the
    ///     transport itself could not complete. Null on the happy path.
    /// </summary>
    public string? Fault { get; init; }
}
