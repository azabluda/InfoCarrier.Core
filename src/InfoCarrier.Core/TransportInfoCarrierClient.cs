// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

/// <summary>
///     An <see cref="IInfoCarrierClient" /> implemented over the <see cref="IInfoCarrierTransport" />
///     seam. Wraps each operation's payload in a versioned <see cref="InfoCarrierEnvelope" />,
///     sends it, and unwraps the response payload.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="TransportInfoCarrierClient" /> class.
/// </remarks>
public sealed class TransportInfoCarrierClient(IInfoCarrierTransport transport, IInfoCarrierSerializer serializer) : IInfoCarrierClient
{
    private readonly IInfoCarrierTransport _transport = transport;
    private readonly IInfoCarrierSerializer _serializer = serializer;

    /// <inheritdoc />
    /// <remarks>
    ///     The one operation that does not round-trip an envelope. Its answer is a sequence, and
    ///     D7's whole point is that the sequence reaches the client as it is produced — so it goes
    ///     out as an envelope and comes back as a stream. The fault check that
    ///     <see cref="RoundTripAsync{TRequest,TResponse}" /> performs happens inside
    ///     <see cref="QueryStreamReader" /> instead, against the same
    ///     <see cref="InfoCarrierFaultMapper" />.
    /// </remarks>
    public async Task<QueryDataResult> QueryDataAsync(
        QueryDataRequest request,
        Microsoft.EntityFrameworkCore.DbContext clientContext,
        CancellationToken cancellationToken = default)
    {
        var envelope = new InfoCarrierEnvelope
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = InfoCarrierOperation.Query,
            Payload = await _serializer.SerializeAsync(request, cancellationToken).ConfigureAwait(false),
        };

        return await _transport.SendQueryAsync(envelope, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SaveChangesResult> SaveChangesAsync(
        SaveChangesRequest request,
        Microsoft.EntityFrameworkCore.DbContext clientContext,
        CancellationToken cancellationToken = default)
        => await RoundTripAsync<SaveChangesRequest, SaveChangesResult>(
            InfoCarrierOperation.SaveChanges, request, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        // `null`, not `new { }`. BeginTransaction takes no arguments, so the payload is a
        // placeholder either way and the server never reads it -- but an anonymous type declared
        // as `object` is serialized by its RUNTIME type, which a source-generated context cannot
        // have metadata for. It failed outright once the resolver was set:
        // "JsonTypeInfo metadata for type '<>f__AnonymousType0' was not provided". `null` is what
        // every other void operation already puts there.
        => await RoundTripAsync<object?, TransactionResult>(
            InfoCarrierOperation.BeginTransaction, null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => await RoundTripAsync<string, object>(
            InfoCarrierOperation.CommitTransaction, transactionId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => await RoundTripAsync<string, object>(
            InfoCarrierOperation.RollbackTransaction, transactionId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => await RoundTripAsync<SavepointRequest, object>(
            InfoCarrierOperation.CreateSavepoint,
            new SavepointRequest { TransactionId = transactionId, Name = name },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => await RoundTripAsync<SavepointRequest, object>(
            InfoCarrierOperation.RollbackToSavepoint,
            new SavepointRequest { TransactionId = transactionId, Name = name },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => await RoundTripAsync<SavepointRequest, object>(
            InfoCarrierOperation.ReleaseSavepoint,
            new SavepointRequest { TransactionId = transactionId, Name = name },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
        => await RoundTripAsync<string, bool>(
            InfoCarrierOperation.SupportsSavepoints, transactionId, cancellationToken).ConfigureAwait(false);

    private async Task<TResponse> RoundTripAsync<TRequest, TResponse>(
        InfoCarrierOperation operation,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var envelope = new InfoCarrierEnvelope
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = operation,
            Payload = await _serializer.SerializeAsync(request, cancellationToken).ConfigureAwait(false),
        };

        InfoCarrierEnvelope response = await _transport.SendAsync(envelope, cancellationToken).ConfigureAwait(false);

        // The fault first, and before the payload is looked at (wire-protocol W5). A failed
        // response carries a placeholder payload, so reading it first would deserialize a null
        // and report the failure as an empty result — which is the one way an error can do more
        // damage than the operation that caused it.
        if (response.Fault is { } fault)
        {
            throw InfoCarrierFaultMapper.Rehydrate(fault);
        }

        return (await _serializer.DeserializeAsync<TResponse>(response.Payload, cancellationToken).ConfigureAwait(false))!;
    }
}
