// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>
///     In-process <see cref="IInfoCarrierServer" />: executes InfoCarrier operations against
///     a real EF Core <see cref="Microsoft.EntityFrameworkCore.DbContext" /> resolved from DI.
///     This is the server half of the in-process test transport.
/// </summary>
/// <remarks>
///     Query rebinding and execution run through <see cref="ServerQueryExecutor" />, and
///     SaveChanges replay through <see cref="ServerSaveChangesExecutor" />. The server resolves
///     the context and serializer per request from DI (DI-first, requirements §4.2) — except
///     inside a transaction, which pins one context across several requests; see
///     <see cref="BeginTransactionAsync" />.
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="InProcessInfoCarrierServer" /> class.
/// </remarks>
public sealed class InProcessInfoCarrierServer(IServiceProvider serviceProvider) : IInfoCarrierServer
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <summary>
    ///     The transactions this server is holding open, by token (wire-protocol W3).
    /// </summary>
    private readonly ConcurrentDictionary<string, OpenTransaction> _transactions = new(StringComparer.Ordinal);

    /// <summary>
    ///     The server half of the application's value-mapper chain
    ///     (<see cref="ValueMapping.IInfoCarrierValueMapper" />).
    /// </summary>
    /// <remarks>
    ///     Resolved from the root provider, not a request scope: a mapper converts between a CLR
    ///     type and a wire primitive and has nothing to hold per request. Empty unless the
    ///     application registered one, which is the no-op the seam guarantees.
    /// </remarks>
    private IEnumerable<ValueMapping.IInfoCarrierValueMapper> ValueMappers
        => _serviceProvider.GetServices<ValueMapping.IInfoCarrierValueMapper>();

    /// <summary>
    ///     The extra CLR types this server permits a payload to name
    ///     (<see cref="Expressions.IInfoCarrierAllowedTypes" />).
    /// </summary>
    /// <remarks>
    ///     From the root provider for the same reason the mappers are: a list of types has nothing
    ///     to hold per request. Empty unless the application registered some, which is the closed
    ///     default ADR-008 constraint 2 requires.
    /// </remarks>
    private IEnumerable<Type> AllowedTypes
        => _serviceProvider.GetServices<Expressions.IInfoCarrierAllowedTypes>().SelectMany(a => a.Types);

    /// <summary>
    ///     Whether this server permits a payload to carry SQL it will execute
    ///     (<see cref="IInfoCarrierArbitrarySqlExecution" />, #60).
    /// </summary>
    /// <remarks>
    ///     From the root provider, like the two above, and absent unless the application
    ///     registered it. <b>This is the security boundary for raw SQL</b> - see
    ///     <c>docs/security-review.md</c> section 5a.
    /// </remarks>
    private bool ArbitrarySqlAllowed
        => _serviceProvider.GetService<IInfoCarrierArbitrarySqlExecution>() is not null;

    /// <summary>
    ///     How this server rebuilds EF's relational raw-SQL query roots
    ///     (<see cref="Metadata.IInfoCarrierRelationalQueryRoots" />, #97).
    /// </summary>
    /// <remarks>
    ///     From the root provider, like the three above, and absent unless the application
    ///     registered <c>AddInfoCarrierRelational()</c> from the
    ///     <c>InfoCarrier.Core.Relational</c> package. <b>This provider, and not the context's:</b>
    ///     the context builds its own internal service provider and never sees the application's
    ///     collection, so a lookup through the context answers <c>null</c> for a server that has
    ///     registered one. <b>It grants nothing</b> — <see cref="ArbitrarySqlAllowed" /> above is
    ///     the boundary and still defaults to refusing.
    /// </remarks>
    private Metadata.IInfoCarrierRelationalQueryRoots? RelationalQueryRoots
        => _serviceProvider.GetService<Metadata.IInfoCarrierRelationalQueryRoots>();

    /// <inheritdoc />
    public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Lease lease = Acquire(request.TransactionId);
        try
        {
            ExpressionSerializer serializer = ExpressionSerializer.CreateForModel(lease.Context.Model, ValueMappers, AllowedTypes);
            var executor = new ServerQueryExecutor(
                lease.Context, serializer, ArbitrarySqlAllowed, RelationalQueryRoots);
            return await executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Lease lease = Acquire(request.TransactionId);
        try
        {
            ExpressionSerializer serializer = ExpressionSerializer.CreateForModel(lease.Context.Model, ValueMappers, AllowedTypes);
            var executor = new ServerSaveChangesExecutor(
                lease.Context, (Expressions.DynamicValueMapper)serializer.ValueMapper);
            return await executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         A transaction spans requests, so the scope it lives in has to outlive the request
    ///         that opened it — the ordinary path creates a scope per request and disposes it on
    ///         the way out. The scope, its context and the store transaction are held here under
    ///         a token, and every later request naming that token runs on that same context
    ///         instead of a fresh one. That token *is* the wire-protocol W3 answer: an open
    ///         transaction cannot be implied by a connection when the transport may be stateless.
    ///     </para>
    ///     <para>
    ///         Whether the transaction is <em>real</em> is the store's business, not this
    ///         server's. A provider that does not do transactions — EF's InMemory one — raises
    ///         its own `TransactionIgnoredWarning` and hands back a stub, and relaying that is
    ///         the honest answer: the client asked the store for a transaction and the store
    ///         said no. The alternative, which this replaces, was the *client* pretending on the
    ///         store's behalf.
    ///     </para>
    /// </remarks>
    public async Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<DbContext>();
            IDbContextTransaction transaction = await context.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            string token = Guid.NewGuid().ToString("N");
            _transactions[token] = new OpenTransaction(scope, context, transaction);
            return new TransactionResult { TransactionId = token };
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => EndAsync(transactionId, commit: true, cancellationToken);

    /// <inheritdoc />
    public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => EndAsync(transactionId, commit: false, cancellationToken);

    /// <inheritdoc />
    public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => Open(transactionId).Transaction.CreateSavepointAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => Open(transactionId).Transaction.RollbackToSavepointAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => Open(transactionId).Transaction.ReleaseSavepointAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
        => Task.FromResult(Open(transactionId).Transaction.SupportsSavepoints);

    private OpenTransaction Open(string transactionId)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        return _transactions.TryGetValue(transactionId, out OpenTransaction? open)
            ? open
            : throw new InvalidOperationException(
                $"Transaction '{transactionId}' is not open on this server. It was committed, "
                    + "rolled back, or belongs to a different server.");
    }

    private async Task EndAsync(string transactionId, bool commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        if (!_transactions.TryRemove(transactionId, out OpenTransaction? open))
        {
            // Already ended, or never this server's. Not an error: a client that rolls back on
            // disposal after an explicit commit is following the ordinary `using` pattern, and
            // EF's own transaction objects tolerate exactly that.
            return;
        }

        try
        {
            if (commit)
            {
                await open.Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await open.Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await open.Transaction.DisposeAsync().ConfigureAwait(false);
            await open.Scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     The context to run one request on: the transaction's, if the request names one,
    ///     otherwise a fresh per-request scope.
    /// </summary>
    /// <remarks>
    ///     An unknown token is refused rather than quietly run outside the transaction. Falling
    ///     back would commit work the caller believes is provisional, which is the one failure
    ///     mode a transaction exists to prevent.
    /// </remarks>
    private Lease Acquire(string? transactionId)
    {
        if (transactionId is null)
        {
            AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            return new Lease(scope.ServiceProvider.GetRequiredService<DbContext>(), scope);
        }

        OpenTransaction open = Open(transactionId);

        // A transaction pins the *connection*, not the change tracker. Every request is
        // self-contained — the client sends the state it needs — and the ordinary path gets a
        // fresh context precisely so nothing carries over. Reusing this one without clearing let
        // one request's tracked entities meet the next request's copy of the same rows: "the
        // instance of entity type 'Driver' cannot be tracked because another instance with the
        // same key value is already being tracked."
        open.Context.ChangeTracker.Clear();
        return new Lease(open.Context, ownedScope: null);
    }

    private sealed record OpenTransaction(AsyncServiceScope Scope, DbContext Context, IDbContextTransaction Transaction);

    /// <summary>
    ///     One request's context, plus the scope to dispose afterwards — but only if this request
    ///     is what created it. A transaction's scope outlives every request that uses it.
    /// </summary>
    private readonly struct Lease(DbContext context, AsyncServiceScope? ownedScope)
    {
        public DbContext Context { get; } = context;

        public ValueTask DisposeAsync()
            => ownedScope?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
