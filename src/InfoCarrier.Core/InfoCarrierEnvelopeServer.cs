// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly ILogger<InfoCarrierEnvelopeServer> _logger = NullLogger<InfoCarrierEnvelopeServer>.Instance;

    /// <summary>Creates a dispatcher with the host's class logger.</summary>
    /// <param name="server">The operation server.</param>
    /// <param name="serializer">The payload serializer.</param>
    /// <param name="logger">The logger from the current host scope.</param>
    public InfoCarrierEnvelopeServer(IInfoCarrierServer server, IInfoCarrierSerializer serializer,
        ILogger<InfoCarrierEnvelopeServer> logger) : this(server, serializer)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Handles one request envelope and produces the response envelope.
    /// </summary>
    public async Task<InfoCarrierEnvelope> DispatchAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string phase = "operation execution";
        string operation = Enum.IsDefined(request.Operation) ? request.Operation.ToString() : "Unknown";

        if (request.ProtocolVersion != InfoCarrierEnvelope.CurrentProtocolVersion)
        {
            Log(LogLevel.Warning, 35002, "InfoCarrier {Operation} was rejected because its protocol version is unsupported.", operation);
            throw new NotSupportedException(
                $"InfoCarrier protocol version {request.ProtocolVersion} is not supported by this "
                + $"server, which speaks version {InfoCarrierEnvelope.CurrentProtocolVersion}.");
        }

        try
        {
            return await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log(cancellationToken.IsCancellationRequested ? LogLevel.Debug : LogLevel.Error, 35001,
                cancellationToken.IsCancellationRequested ? "InfoCarrier {Operation} was cancelled." : "InfoCarrier {Operation} stopped with unexpected cancellation.", operation);
            throw;
        }
        catch (Exception exception)
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
            string failedPhase = phase;
            InfoCarrierEnvelope response;
            try
            {
                phase = "fault response serialization";
                response = request with
                {
                    Payload = _serializer.Serialize<object?>(null),
                    Fault = InfoCarrierFaultMapper.Capture(exception),
                };
            }
            catch (Exception)
            {
                Log(LogLevel.Error, 35001, "InfoCarrier {Operation} failed while constructing its fault response.", operation);
                throw;
            }

            Log(exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ? LogLevel.Information : LogLevel.Error,
                35001, "InfoCarrier {Operation} failed during {Phase}.", operation, failedPhase);
            return response;
        }

        async Task<InfoCarrierEnvelope> ExecuteAsync(
            InfoCarrierEnvelope request,
            CancellationToken cancellationToken)
        {
            phase = "operation execution";
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

        T Payload<T>(InfoCarrierEnvelope request)
        {
            phase = "request payload deserialization";
            T payload = _serializer.Deserialize<T>(request.Payload)
                ?? throw new InvalidOperationException($"The {request.Operation} envelope carried no {typeof(T).Name} payload.");
            phase = "operation execution";
            return payload;
        }

        async Task<InfoCarrierEnvelope> RespondAsync<TResult>(
            InfoCarrierEnvelope request, Task<TResult> operation)
        {
            TResult result = await operation.ConfigureAwait(false);
            phase = "result serialization after operation completion";
            return request with { Payload = _serializer.Serialize(result) };
        }

        // <summary>
        //     A response to an operation that returns nothing. The payload is still written, because
        //     the client deserializes one either way and a zero-length body is not valid JSON.
        // </summary>
        async Task<InfoCarrierEnvelope> AcknowledgeAsync(InfoCarrierEnvelope request, Task operation)
        {
            await operation.ConfigureAwait(false);
            phase = "result serialization after operation completion";
            return request with { Payload = _serializer.Serialize<object?>(null) };
        }

        Task<InfoCarrierEnvelope> SavepointAsync(
            InfoCarrierEnvelope request,
            Func<string, string, CancellationToken, Task> operation,
            CancellationToken cancellationToken)
        {
            SavepointRequest savepoint = Payload<SavepointRequest>(request);
            return AcknowledgeAsync(
                request, operation(savepoint.TransactionId, savepoint.Name, cancellationToken));
        }
    }

    private void Log(LogLevel level, int eventId, string message, params object?[] values)
    {
        // The exception and untrusted request fields are deliberately absent.
        try { _logger.Log(level, new EventId(eventId), message, values); }
        catch (Exception) { }
    }
}
