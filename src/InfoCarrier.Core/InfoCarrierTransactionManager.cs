// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side transaction manager: begin, commit and rollback are round trips to the server
///     (requirements §2.9, milestone M4). The open transaction is named by the wire-protocol W3
///     token, which every request belonging to it carries.
/// </summary>
/// <remarks>
///     <para>
///         Nothing is decided locally. The client asks the store — which is on the far side — to
///         begin a transaction, and whatever the store does is what happens. A backend that does
///         not do transactions, EF's InMemory provider being the one in this repo, raises its own
///         <c>TransactionIgnoredWarning</c> there and hands back a stub, so the client ends up
///         holding a transaction that does nothing. That is the same *outcome* as before M4 and a
///         very different arrangement: the warning now comes from the component that actually
///         refused, rather than from a client guessing on its behalf.
///     </para>
///     <para>
///         Superseding the previous design is deliberate. This class used to return a stub and
///         raise <c>InfoCarrierEventId.TransactionIgnoredWarning</c> itself, defaulted to
///         <c>WarningBehavior.Throw</c>, because the server had no way to hold a transaction open
///         across requests. It has one now.
///     </para>
///     <para>
///         One transaction at a time, as EF's own managers do —
///         <c>BeginTransaction</c> on a context that already has one is an error, not a nesting
///         request. Savepoints are the nesting mechanism and are a separate interface.
///     </para>
/// </remarks>
public class InfoCarrierTransactionManager : IDbContextTransactionManager
{
    private readonly IInfoCarrierClient _client;
    private InfoCarrierTransaction? _current;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierTransactionManager" /> class.
    /// </summary>
    public InfoCarrierTransactionManager(IDbContextOptions options)
        => _client = options.Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .First()
            .InfoCarrierClient!;

    /// <inheritdoc />
    public virtual IDbContextTransaction? CurrentTransaction => _current;

    /// <inheritdoc />
    public virtual IDbContextTransaction BeginTransaction()
        => BeginTransactionAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_current is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already open on this context. Commit or roll it back before "
                    + "beginning another; nested transactions are expressed as savepoints.");
        }

        Common.TransactionResult result = await _client
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        _current = new InfoCarrierTransaction(_client, result.TransactionId, () => _current = null);
        return _current;
    }

    /// <summary>
    ///     Runs this context's requests inside a transaction another context began, named by its
    ///     server token (wire-protocol W3).
    /// </summary>
    /// <remarks>
    ///     The token is the whole point of a token: it identifies a server transaction
    ///     independently of the connection that opened it, so a second context can join. The
    ///     result is *not owned* — committing or disposing it detaches this context and leaves
    ///     the transaction to whoever began it.
    /// </remarks>
    public virtual IDbContextTransaction UseTransaction(string serverTransactionId)
    {
        ArgumentNullException.ThrowIfNull(serverTransactionId);

        _current = new InfoCarrierTransaction(
            _client, serverTransactionId, () => _current = null, owned: false);
        return _current;
    }

    /// <inheritdoc />
    public virtual void CommitTransaction()
        => CommitTransactionAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await Require().CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual void RollbackTransaction()
        => RollbackTransactionAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        await Require().RollbackAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     No implicit transaction: a single <c>SaveChanges</c> is one request, and the server
    ///     runs it in its own store transaction. Returning one here would wrap a round trip that
    ///     is already atomic.
    /// </remarks>
    public virtual IDbContextTransaction? OnSaveChanges()
        => null;

    /// <inheritdoc />
    /// <remarks>
    ///     Called when the context is being reset for reuse (pooling) or disposed. Anything still
    ///     open is rolled back: the server is holding a scope and a store transaction for it, and
    ///     a context that is going away is never going to commit.
    /// </remarks>
    public virtual void ResetState()
        => ResetStateAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual async Task ResetStateAsync(CancellationToken cancellationToken = default)
    {
        if (_current is { } transaction)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private InfoCarrierTransaction Require()
        => _current ?? throw new InvalidOperationException(
            "No transaction is open on this context.");
}
