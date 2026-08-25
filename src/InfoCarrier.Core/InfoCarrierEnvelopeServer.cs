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
/// <remarks>
///     Initializes a new instance of the <see cref="InfoCarrierEnvelopeServer" /> class.
/// </remarks>
/// <param name="server">The server the operations run against.</param>
/// <param name="serializer">
///     The serializer for payloads. This is the deserializing side of a remote caller's bytes,
///     so it is where <see cref="InfoCarrierPayloadLimits" /> earns its keep.
/// </param>
public sealed class InfoCarrierEnvelopeServer(IInfoCarrierServer server, IInfoCarrierSerializer serializer)
{
    private readonly IInfoCarrierServer _server = server;
    private readonly IInfoCarrierSerializer _serializer = serializer;

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

        try
        {
            return await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // **A failure is a result, not an escape** (wire-protocol W5). In-process an exception
            // reaches the caller by propagating; no network transport can do that, so it has to
            // travel as data in the response and be raised again on the other side. Routing it
            // through the envelope here is what makes the suite test the wire's error behaviour
            // rather than the absence of a wire.
            //
            // The version check above is deliberately outside this: a version mismatch means the
            // two ends do not agree what an envelope *is*, so answering with one is optimistic.
            //
            // `OperationCanceledException` is excluded because cancellation is not a server-side
            // failure to report — it is the caller's own token, and the caller is entitled to see
            // its own `OperationCanceledException` rather than a rebuilt copy. W6 closed on that
            // footing: the server stops the query when the token trips, and the exception stays
            // the caller's.
            return request with
            {
                Payload = _serializer.Serialize<object?>(null),
                Fault = InfoCarrierFaultMapper.Capture(exception),
            };
        }
    }

    private async Task<InfoCarrierEnvelope> ExecuteAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken)
        => request.Operation switch
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
