// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

/// <summary>
///     The server half of the envelope protocol (wire-protocol §1): unwraps an
///     <see cref="InfoCarrierEnvelope" />, checks its <see cref="InfoCarrierEnvelope.ProtocolVersion" />,
///     dispatches the operation to an <see cref="IInfoCarrierServer" />, and wraps the answer
///     back up.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this exists.</b> <see cref="TransportInfoCarrierClient" /> has wrapped every
///         request in an envelope since it was written, and nothing in the product ever unwrapped
///         one — the only dispatcher was six cases short and inline in a smoke test. So a network
///         transport author had a client to call and no server to answer it, and the envelope and
///         the protocol version were, in practice, write-only fields. Milestone M5 lists them as
///         an exit criterion for exactly that reason.
///     </para>
///     <para>
///         <b>The version check is the point of a version field.</b> A field carried and never
///         read is documentation, not a compatibility mechanism. This refuses a major version it
///         does not implement, and says both numbers — a client one release ahead should learn
///         that from the first response, not from a deserialization error deep in a payload whose
///         shape it no longer shares.
///     </para>
/// </remarks>
public sealed class InfoCarrierEnvelopeServer
{
    private readonly IInfoCarrierServer _server;
    private readonly IInfoCarrierSerializer _serializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierEnvelopeServer" /> class.
    /// </summary>
    /// <param name="server">The server the operations run against.</param>
    /// <param name="serializer">
    ///     The serializer for payloads. This is the deserializing side of a remote caller's bytes,
    ///     so it is where <see cref="InfoCarrierPayloadLimits" /> earns its keep.
    /// </param>
    public InfoCarrierEnvelopeServer(IInfoCarrierServer server, IInfoCarrierSerializer serializer)
    {
        _server = server;
        _serializer = serializer;
    }

    /// <summary>
    ///     Handles one request envelope and produces the response envelope.
    /// </summary>
    public async Task<InfoCarrierEnvelope> DispatchAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProtocolVersion != InfoCarrierEnvelope.CurrentProtocolVersion)
        {
            throw new NotSupportedException(
                $"InfoCarrier protocol version {request.ProtocolVersion} is not supported by this "
                + $"server, which speaks version {InfoCarrierEnvelope.CurrentProtocolVersion}.");
        }

        return request.Operation switch
        {
            InfoCarrierOperation.Query
                => await RespondAsync(
                    request,
                    _server.QueryDataAsync(Payload<QueryDataRequest>(request), cancellationToken))
                    .ConfigureAwait(false),

            InfoCarrierOperation.SaveChanges
                => await RespondAsync(
                    request,
                    _server.SaveChangesAsync(Payload<SaveChangesRequest>(request), cancellationToken))
                    .ConfigureAwait(false),

            InfoCarrierOperation.BeginTransaction
                => await RespondAsync(request, _server.BeginTransactionAsync(cancellationToken))
                    .ConfigureAwait(false),

            InfoCarrierOperation.CommitTransaction
                => await AcknowledgeAsync(
                    request, _server.CommitTransactionAsync(Payload<string>(request), cancellationToken))
                    .ConfigureAwait(false),

            InfoCarrierOperation.RollbackTransaction
                => await AcknowledgeAsync(
                    request, _server.RollbackTransactionAsync(Payload<string>(request), cancellationToken))
                    .ConfigureAwait(false),

            InfoCarrierOperation.CreateSavepoint
                => await SavepointAsync(request, _server.CreateSavepointAsync, cancellationToken)
                    .ConfigureAwait(false),

            InfoCarrierOperation.RollbackToSavepoint
                => await SavepointAsync(request, _server.RollbackToSavepointAsync, cancellationToken)
                    .ConfigureAwait(false),

            InfoCarrierOperation.ReleaseSavepoint
                => await SavepointAsync(request, _server.ReleaseSavepointAsync, cancellationToken)
                    .ConfigureAwait(false),

            InfoCarrierOperation.SupportsSavepoints
                => await RespondAsync(
                    request,
                    _server.SupportsSavepointsAsync(Payload<string>(request), cancellationToken))
                    .ConfigureAwait(false),

            // Default-deny, like everything else the wire admits: an operation this server does
            // not implement is refused by name rather than silently treated as one it does.
            _ => throw new NotSupportedException(
                $"InfoCarrier operation '{request.Operation}' is not supported by this server."),
        };
    }

    private T Payload<T>(InfoCarrierEnvelope request)
        => _serializer.Deserialize<T>(request.Payload)
            ?? throw new InvalidOperationException(
                $"The {request.Operation} envelope carried no {typeof(T).Name} payload.");

    private async Task<InfoCarrierEnvelope> RespondAsync<TResult>(
        InfoCarrierEnvelope request, Task<TResult> operation)
        => request with { Payload = _serializer.Serialize(await operation.ConfigureAwait(false)) };

    /// <summary>
    ///     A response to an operation that returns nothing. The payload is still written, because
    ///     the client deserializes one either way and a zero-length body is not valid JSON.
    /// </summary>
    private async Task<InfoCarrierEnvelope> AcknowledgeAsync(InfoCarrierEnvelope request, Task operation)
    {
        await operation.ConfigureAwait(false);
        return request with { Payload = _serializer.Serialize<object?>(null) };
    }

    private Task<InfoCarrierEnvelope> SavepointAsync(
        InfoCarrierEnvelope request,
        Func<string, string, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        SavepointRequest savepoint = Payload<SavepointRequest>(request);
        return AcknowledgeAsync(
            request, operation(savepoint.TransactionId, savepoint.Name, cancellationToken));
    }
}
