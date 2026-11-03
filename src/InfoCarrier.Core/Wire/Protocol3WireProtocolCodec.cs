// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json.Serialization;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;
using Old = InfoCarrier.Core.Wire.V3.Common;
using OldFrames = InfoCarrier.Core.Wire.V3.Query;

namespace InfoCarrier.Core.Wire;

/// <summary>Owns protocol 3 serialization, operation admission, and conversion to live types.</summary>
public sealed class Protocol3WireProtocolCodec : IWireProtocolCodec
{
    /// <inheritdoc />
    public int Version => Old.InfoCarrierEnvelope.CurrentProtocolVersion;
    /// <inheritdoc />
    public JsonSerializerContext JsonContext => V3.Protocol3JsonContext.Default;

    /// <inheritdoc />
    public bool IsKnownOperation(InfoCarrierOperation operation)
        => operation is InfoCarrierOperation.Query or InfoCarrierOperation.SaveChanges or InfoCarrierOperation.BeginTransaction
            or InfoCarrierOperation.CommitTransaction or InfoCarrierOperation.RollbackTransaction or InfoCarrierOperation.CreateSavepoint
            or InfoCarrierOperation.RollbackToSavepoint or InfoCarrierOperation.ReleaseSavepoint or InfoCarrierOperation.SupportsSavepoints;

    /// <inheritdoc />
    public async ValueTask<InfoCarrierEnvelope?> ReadEnvelopeAsync(IInfoCarrierSerializer serializer, Stream source, CancellationToken token)
    {
        Old.InfoCarrierEnvelope? old = await serializer.DeserializeAsync<Old.InfoCarrierEnvelope>(source, token).ConfigureAwait(false);
        return old is null ? null : new InfoCarrierEnvelope
        {
            ProtocolVersion = old.ProtocolVersion, Operation = ToLive(old.Operation), Payload = old.Payload,
            CorrelationId = old.CorrelationId, Fault = Protocol3Mapping.ToLive(old.Fault),
        };
    }

    /// <summary>Decodes an operation using its already admitted contract.</summary>
    /// <inheritdoc />
    public T? Deserialize<T>(IInfoCarrierSerializer serializer, byte[] payload, InfoCarrierPayloadLimits limits)
    {
        object? result = typeof(T) == typeof(QueryDataRequest) ? Protocol3Mapping.ToLive(Required(serializer.Deserialize<Old.QueryDataRequest>(payload)), limits)
            : typeof(T) == typeof(QueryDataResult) ? Protocol3Mapping.ToLive(Required(serializer.Deserialize<Old.QueryDataResult>(payload)))
            : typeof(T) == typeof(SaveChangesRequest) ? Protocol3Mapping.ToLive(Required(serializer.Deserialize<Old.SaveChangesRequest>(payload)))
            : typeof(T) == typeof(SaveChangesResult) ? Protocol3Mapping.ToLive(Required(serializer.Deserialize<Old.SaveChangesResult>(payload)))
            : typeof(T) == typeof(SavepointRequest) ? LiveSavepoint(Required(serializer.Deserialize<Old.SavepointRequest>(payload)))
            : typeof(T) == typeof(TransactionResult) ? new TransactionResult { TransactionId = Required(serializer.Deserialize<Old.TransactionResult>(payload)).TransactionId }
            : serializer.Deserialize<T>(payload);
        return (T?)result;
    }

    /// <summary>Encodes a response using the requesting peer's contract.</summary>
    /// <inheritdoc />
    public byte[] Serialize<T>(IInfoCarrierSerializer serializer, T value)
    {
        return value switch
        {
            QueryDataRequest x => serializer.Serialize(Protocol3Mapping.ToOld(x)),
            QueryDataResult x => serializer.Serialize(Protocol3Mapping.ToOld(x)),
            SaveChangesRequest x => serializer.Serialize(Protocol3Mapping.ToOld(x)),
            SaveChangesResult x => serializer.Serialize(Protocol3Mapping.ToOld(x)),
            SavepointRequest x => serializer.Serialize(new Old.SavepointRequest { TransactionId = x.TransactionId, Name = x.Name }),
            TransactionResult x => serializer.Serialize(new Old.TransactionResult { TransactionId = x.TransactionId }),
            InfoCarrierEnvelope x => serializer.Serialize(new Old.InfoCarrierEnvelope
            {
                ProtocolVersion = x.ProtocolVersion, Payload = x.Payload, CorrelationId = x.CorrelationId,
                Operation = ToOld(x.Operation), Fault = Protocol3Mapping.ToOld(x.Fault),
            }),
            _ => serializer.Serialize(value),
        };
    }

    /// <inheritdoc />
    public async ValueTask<QueryDataRequest?> ReadQueryAsync(IInfoCarrierSerializer serializer,
        Stream source, InfoCarrierPayloadLimits limits, CancellationToken token)
    {
        Old.QueryDataRequest? request = await serializer.DeserializeAsync<Old.QueryDataRequest>(source, token).ConfigureAwait(false);
        if (request is null) { return null; }
        if (request.ProtocolVersion != Version) { throw new InfoCarrierProtocolException(request.ProtocolVersion, InfoCarrierEnvelope.CurrentProtocolVersion); }
        return Protocol3Mapping.ToLive(request, limits);
    }

    /// <inheritdoc />
    public ValueTask WriteFramesAsync(Stream destination, IAsyncEnumerable<QueryStreamFrame> frames, CancellationToken token)
        => OldFrames.QueryStreamFrameCodec.WriteAsync(destination, OldSequence(frames, token), token);

    private static async IAsyncEnumerable<OldFrames.QueryStreamFrame> OldSequence(IAsyncEnumerable<QueryStreamFrame> frames,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (QueryStreamFrame frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return frame switch
            {
                QueryStartFrame x => new OldFrames.QueryStartFrame(3, x.Slot, x.IsEntityResult, x.ElementTypeName),
                QueryRowFrame x => new OldFrames.QueryRowFrame(Protocol3Mapping.ToOld(x.Value)),
                QueryLogFrame x => new OldFrames.QueryLogFrame(Protocol3Mapping.ToOld(x.Event)),
                QueryFaultFrame x => new OldFrames.QueryFaultFrame(Protocol3Mapping.ToOld(x.Fault)),
                QueryCompleteFrame => new OldFrames.QueryCompleteFrame(),
                _ => throw new NotSupportedException("Query frame has no protocol 3 representation."),
            };
        }
    }

    private static SavepointRequest LiveSavepoint(Old.SavepointRequest x) => new() { TransactionId = x.TransactionId, Name = x.Name };
    private static T Required<T>(T? value) where T : class => value ?? throw new System.Text.Json.JsonException("The operation payload is null.");

    private static Old.InfoCarrierOperation ToOld(InfoCarrierOperation operation) => operation switch
    {
        InfoCarrierOperation.Query => Old.InfoCarrierOperation.Query,
        InfoCarrierOperation.SaveChanges => Old.InfoCarrierOperation.SaveChanges,
        InfoCarrierOperation.BeginTransaction => Old.InfoCarrierOperation.BeginTransaction,
        InfoCarrierOperation.CommitTransaction => Old.InfoCarrierOperation.CommitTransaction,
        InfoCarrierOperation.RollbackTransaction => Old.InfoCarrierOperation.RollbackTransaction,
        InfoCarrierOperation.CreateSavepoint => Old.InfoCarrierOperation.CreateSavepoint,
        InfoCarrierOperation.RollbackToSavepoint => Old.InfoCarrierOperation.RollbackToSavepoint,
        InfoCarrierOperation.ReleaseSavepoint => Old.InfoCarrierOperation.ReleaseSavepoint,
        InfoCarrierOperation.SupportsSavepoints => Old.InfoCarrierOperation.SupportsSavepoints,
        // Unknown numeric identifiers are opaque fault-echo data, never an execution grant.
        // Generated scalar metadata preserves them without casting between enum vocabularies.
        _ => System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(operation, InfoCarrierJsonContext.Default.InfoCarrierOperation),
            V3.Protocol3JsonContext.Default.InfoCarrierOperation),
    };

    private static InfoCarrierOperation ToLive(Old.InfoCarrierOperation operation) => operation switch
    {
        Old.InfoCarrierOperation.Query => InfoCarrierOperation.Query,
        Old.InfoCarrierOperation.SaveChanges => InfoCarrierOperation.SaveChanges,
        Old.InfoCarrierOperation.BeginTransaction => InfoCarrierOperation.BeginTransaction,
        Old.InfoCarrierOperation.CommitTransaction => InfoCarrierOperation.CommitTransaction,
        Old.InfoCarrierOperation.RollbackTransaction => InfoCarrierOperation.RollbackTransaction,
        Old.InfoCarrierOperation.CreateSavepoint => InfoCarrierOperation.CreateSavepoint,
        Old.InfoCarrierOperation.RollbackToSavepoint => InfoCarrierOperation.RollbackToSavepoint,
        Old.InfoCarrierOperation.ReleaseSavepoint => InfoCarrierOperation.ReleaseSavepoint,
        Old.InfoCarrierOperation.SupportsSavepoints => InfoCarrierOperation.SupportsSavepoints,
        _ => System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(operation, V3.Protocol3JsonContext.Default.InfoCarrierOperation),
            InfoCarrierJsonContext.Default.InfoCarrierOperation),
    };
}
