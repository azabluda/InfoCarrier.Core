// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json.Serialization;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;

namespace InfoCarrier.Core.Wire;

/// <summary>One contract's encoding and conversion to the live execution representation.</summary>
public interface IWireProtocolCodec
{
    /// <summary>Gets the contract integer implemented by this codec.</summary>
    int Version { get; }
    /// <summary>Gets generated metadata for this contract's operation payloads.</summary>
    JsonSerializerContext JsonContext { get; }
    /// <summary>Checks whether this contract defines the operation.</summary>
    bool IsKnownOperation(InfoCarrierOperation operation);
    /// <summary>Decodes operation bytes into the live execution representation.</summary>
    T? Deserialize<T>(IInfoCarrierSerializer serializer, byte[] payload, InfoCarrierPayloadLimits limits);
    /// <summary>Encodes live operation data for this contract's peer.</summary>
    byte[] Serialize<T>(IInfoCarrierSerializer serializer, T value);
    /// <summary>Reads an envelope while preserving its peer's contract identification.</summary>
    ValueTask<InfoCarrierEnvelope?> ReadEnvelopeAsync(IInfoCarrierSerializer serializer, Stream source, CancellationToken token);
    /// <summary>Reads an admitted query into the live execution representation.</summary>
    ValueTask<QueryDataRequest?> ReadQueryAsync(IInfoCarrierSerializer serializer, Stream source, InfoCarrierPayloadLimits limits, CancellationToken token);
    /// <summary>Writes live frames in this contract's representation.</summary>
    ValueTask WriteFramesAsync(Stream destination, IAsyncEnumerable<QueryStreamFrame> frames, CancellationToken token);
}
