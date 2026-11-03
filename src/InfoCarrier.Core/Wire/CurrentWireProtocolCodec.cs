// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json.Serialization;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;

namespace InfoCarrier.Core.Wire;

/// <summary>The live contract already uses the execution representation.</summary>
public sealed class CurrentWireProtocolCodec : IWireProtocolCodec
{
    /// <inheritdoc />
    public int Version => InfoCarrierEnvelope.CurrentProtocolVersion;
    /// <inheritdoc />
    public JsonSerializerContext JsonContext => InfoCarrierJsonContext.Default;
    /// <inheritdoc />
    public bool IsKnownOperation(InfoCarrierOperation operation) => Enum.IsDefined(operation);
    /// <inheritdoc />
    public T? Deserialize<T>(IInfoCarrierSerializer serializer, byte[] payload, InfoCarrierPayloadLimits limits)
        => serializer.Deserialize<T>(payload);
    /// <inheritdoc />
    public byte[] Serialize<T>(IInfoCarrierSerializer serializer, T value) => serializer.Serialize(value);
    /// <inheritdoc />
    public ValueTask<InfoCarrierEnvelope?> ReadEnvelopeAsync(IInfoCarrierSerializer serializer, Stream source, CancellationToken token)
        => serializer.DeserializeAsync<InfoCarrierEnvelope>(source, token);
    /// <inheritdoc />
    public ValueTask<QueryDataRequest?> ReadQueryAsync(IInfoCarrierSerializer serializer, Stream source, InfoCarrierPayloadLimits limits, CancellationToken token)
        => serializer.DeserializeAsync<QueryDataRequest>(source, token);
    /// <inheritdoc />
    public ValueTask WriteFramesAsync(Stream destination, IAsyncEnumerable<QueryStreamFrame> frames, CancellationToken token)
        => QueryStreamFrameCodec.WriteAsync(destination, frames, token);
}
