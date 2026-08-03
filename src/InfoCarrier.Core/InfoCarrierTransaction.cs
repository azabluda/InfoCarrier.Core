// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     A transaction open on the server, named by the wire-protocol W3 token
///     (<see cref="ServerTransactionId" />) that every request belonging to it carries.
/// </summary>
/// <remarks>
///     <para>
///         Commit and rollback are round trips; there is nothing to do locally, because the store
///         and its transaction are both on the far side. Whether the transaction is <em>real</em>
///         is the server store's business — a backend that ignores transactions raises its own
///         warning and hands back a stub, and this object then names a transaction that does
///         nothing. That is the honest shape: the client no longer decides on the store's behalf.
///     </para>
///     <para>
///         Disposal rolls back unless commit or rollback has already happened, which is what
///         requirements §2.9 asks for and what
///         <c>using (var tx = context.Database.BeginTransaction())</c> means everywhere else in
///         EF. Without it a client that faulted mid-transaction would leave the server holding a
///         scope, a context and an open store transaction indefinitely.
///     </para>
/// </remarks>
public class InfoCarrierTransaction : IDbContextTransaction
{
    private readonly IInfoCarrierClient _client;
    private readonly Action _onFinished;
    private readonly bool _owned;
    private bool _finished;
    private bool? _supportsSavepoints;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierTransaction" /> class.
    /// </summary>
    /// <param name="onFinished">
    ///     Told when this transaction ends, however it ends. A caller may commit through the
    ///     transaction object rather than through the manager —
    ///     <c>using (var tx = context.Database.BeginTransaction()) { … tx.Commit(); }</c> is the
    ///     ordinary shape — and the manager still has to stop reporting it as current. EF's
    ///     relational transaction notifies its manager the same way.
    /// </param>
    /// <param name="owned">
    ///     Whether ending this object ends the server's transaction. False for one *enlisted* in a
    ///     transaction another context began: several contexts may share a server transaction —
    ///     that is what a token is for — and only the one that opened it may end it. An enlisted
    ///     transaction still names the token, which is its whole job, so requests from this
    ///     context run inside it.
    /// </param>
    public InfoCarrierTransaction(
        IInfoCarrierClient client,
        string serverTransactionId,
        Action onFinished,
        bool owned = true)
    {
        _client = client;
        _onFinished = onFinished;
        _owned = owned;
        ServerTransactionId = serverTransactionId;
    }

    /// <summary>
    ///     The server's token for this transaction (wire-protocol W3). Every request that must
    ///     run inside it carries this value.
    /// </summary>
    public virtual string ServerTransactionId { get; }

    /// <inheritdoc />
    public virtual Guid TransactionId { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public virtual void Commit()
        => CommitAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _onFinished();

        if (_owned)
        {
            await _client.CommitTransactionAsync(ServerTransactionId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public virtual void Rollback()
        => RollbackAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _onFinished();

        if (_owned)
        {
            await _client.RollbackTransactionAsync(ServerTransactionId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Savepoints are named within a transaction rather than being scopes of their own, so
    ///     the W3 token plus a name is the whole address — there is no second token to invent.
    ///     Whether they work at all is the server store's answer, not this client's; see
    ///     <see cref="SupportsSavepoints" />.
    /// </remarks>
    public virtual void CreateSavepoint(string name)
        => CreateSavepointAsync(name).GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
        => _client.CreateSavepointAsync(ServerTransactionId, name, cancellationToken);

    /// <inheritdoc />
    public virtual void RollbackToSavepoint(string name)
        => RollbackToSavepointAsync(name).GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
        => _client.RollbackToSavepointAsync(ServerTransactionId, name, cancellationToken);

    /// <inheritdoc />
    public virtual void ReleaseSavepoint(string name)
        => ReleaseSavepointAsync(name).GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
        => _client.ReleaseSavepointAsync(ServerTransactionId, name, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    ///     A round trip, because only the server's store knows. It is cached after the first ask:
    ///     the answer is a property of the store, and EF consults this before every savepoint
    ///     operation.
    /// </remarks>
    public virtual bool SupportsSavepoints
        => _supportsSavepoints ??= _client
            .SupportsSavepointsAsync(ServerTransactionId)
            .GetAwaiter()
            .GetResult();

    /// <inheritdoc />
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        Rollback();
    }

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await RollbackAsync().ConfigureAwait(false);
    }
}
