// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
public sealed class InProcessInfoCarrierServer(IServiceProvider serviceProvider)
    : IInfoCarrierServer, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <summary>
    ///     The transactions this server is holding open, by token (wire-protocol W3).
    /// </summary>
    private readonly ConcurrentDictionary<string, OpenTransaction> _transactions = new(StringComparer.Ordinal);

    /// <summary>
    ///     Identifies THIS server object, and is carried inside every token it mints (#54, part 3).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>It exists to make one failure diagnosable, and it fixes nothing else.</b> The
    ///         registry above is a field, so a token resolves only on the process holding it.
    ///         Behind a load balancer a client can open a transaction on one instance and save on
    ///         another, and refusing that is correct; what was wrong is that the refusal read
    ///         "committed, rolled back, or belongs to a different server", and a reader takes the
    ///         first branch and looks for a bug in their own code.
    ///     </para>
    ///     <para>
    ///         <b>New per object, deliberately, so a RESTART reads correctly too.</b> A restarted
    ///         process is a new instance, so a token minted before it is reported as another
    ///         instance's rather than as work that ended. That is the truth: the transaction it
    ///         named died with the process that held it.
    ///     </para>
    ///     <para>
    ///         <b>It names no machine, user or row</b>, being random per object, so putting it in
    ///         a message discloses nothing a token did not already.
    ///     </para>
    /// </remarks>
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    ///     The timer that sweeps idle transactions, or <see langword="null" /> when no timeout is
    ///     registered or none has been begun yet.
    /// </summary>
    /// <remarks>
    ///     <b>Created on the first <see cref="BeginTransactionAsync" /> rather than in the
    ///     constructor</b>, so a server that never opens a transaction never starts a timer, and a
    ///     server with no timeout registered never creates one at all. Guarded by
    ///     <see cref="_lifecycle" /> because two concurrent first transactions would otherwise
    ///     create two.
    /// </remarks>
    private ITimer? _sweepTimer;

    private readonly object _lifecycle = new();

    private bool _disposed;

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
    ///     From the root provider, like the three above. <b>Usually absent, and that is not a
    ///     gap</b>: a server application registers the endpoint and its own context, not
    ///     <c>AddEntityFrameworkInfoCarrier</c>, so nothing puts this in its collection.
    ///     <see cref="ServerQueryExecutor" /> falls back to the one shipped implementation, so the
    ///     server can always rebuild a raw-SQL root it is permitted to execute. Registering one
    ///     here replaces that fallback.
    ///     <para>
    ///         <b>Corrected 2026-09-09.</b> This said "absent unless the application registered
    ///         <c>AddInfoCarrierRelational()</c> from the <c>InfoCarrier.Core.Relational</c>
    ///         package". R135 deleted that method and there is no such package.
    ///     </para>
    ///     <b>This provider, and not the context's:</b>
    ///     the context builds its own internal service provider and never sees the application's
    ///     collection, so a lookup through the context answers <c>null</c> for a server that has
    ///     registered one. <b>It grants nothing</b> — <see cref="ArbitrarySqlAllowed" /> above is
    ///     the boundary and still defaults to refusing.
    /// </remarks>
    private Metadata.IInfoCarrierRelationalQueryRoots? RelationalQueryRoots
        => _serviceProvider.GetService<Metadata.IInfoCarrierRelationalQueryRoots>();

    /// <summary>
    ///     Whether this server sends the log events it raises back to the client, and from which
    ///     level (<see cref="IInfoCarrierServerLogForwarding" />, R172).
    /// </summary>
    /// <remarks>
    ///     From the root provider, like the four above, and absent unless the application
    ///     registered it. <b>It discloses the server's own diagnostics</b> — see the interface for
    ///     what that costs.
    /// </remarks>
    private IInfoCarrierServerLogForwarding? LogForwarding
        => _serviceProvider.GetService<IInfoCarrierServerLogForwarding>();

    /// <summary>
    ///     Whether this server may forward its log even when the context logs sensitive data
    ///     (<see cref="IInfoCarrierSensitiveServerLogForwarding" />, R172).
    /// </summary>
    private bool SensitiveLogForwardingAllowed
        => _serviceProvider.GetService<IInfoCarrierSensitiveServerLogForwarding>() is not null;

    /// <summary>
    ///     How long a transaction may go untouched before this server rolls it back
    ///     (<see cref="IInfoCarrierServerTransactionTimeout" />, #54).
    /// </summary>
    /// <remarks>
    ///     From the root provider, like the five above, and absent unless the application
    ///     registered it. <b>Absent means never evict</b>, which is how every version up to
    ///     10.1.0 behaved and is what makes this opt-in.
    /// </remarks>
    private IInfoCarrierServerTransactionTimeout? TransactionTimeout
        => _serviceProvider.GetService<IInfoCarrierServerTransactionTimeout>();

    /// <summary>
    ///     The clock the idle timeout is measured against.
    /// </summary>
    /// <remarks>
    ///     <b>From the root provider, defaulting to the real one</b>, which is the same shape as
    ///     every other optional service here and is what lets a test drive eviction without
    ///     sleeping. <c>FakeTimeProvider.Advance</c> fires a <c>CreateTimer</c> callback synchronously on
    ///     the calling thread, so a test asserts immediately after advancing.
    ///     <para>
    ///         <b><c>CreateTimer</c> and not <c>PeriodicTimer</c>, and the difference is measured.</b> A
    ///         <c>PeriodicTimer</c> loop is released by <c>Advance</c> too, but on a background task, so a
    ///         test would have to wait for the loop body and that is a race. A timer callback runs
    ///         inline.
    ///     </para>
    /// </remarks>
    private TimeProvider Clock
        => _serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

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

            using ServerLogCapture.Scope? capture = BeginLogCapture();
            QueryDataResult result = await executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

            return CollectedLog(capture, lease.Context) is { } log
                ? result with { ServerLog = log }
                : result;
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

            using ServerLogCapture.Scope? capture = BeginLogCapture();
            SaveChangesResult result = await executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

            return CollectedLog(capture, lease.Context) is { } log
                ? result with { ServerLog = log }
                : result;
        }
        finally
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Opens a capture for this request, or <see langword="null" /> when the server has not
    ///     granted log forwarding.
    /// </summary>
    private ServerLogCapture.Scope? BeginLogCapture()
        => LogForwarding is { } forwarding ? ServerLogCapture.Begin(forwarding.MinimumLevel) : null;

    /// <summary>
    ///     What a finished request may send back, after the sensitive-logging gate.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The gate is all-or-nothing and that is deliberate.</b>
    ///         <c>EnableSensitiveDataLogging</c> is not a per-event flag: it changes what EF's
    ///         message templates say across the board, so an event raised by a context that has it
    ///         on may carry key, parameter or property values in its text and nothing distinguishes
    ///         the ones that do. A server whose context logs sensitively therefore forwards
    ///         everything or nothing, and only the second grant chooses the first.
    ///     </para>
    ///     <para>
    ///         Read off <see cref="CoreOptionsExtension" /> rather than <c>ILoggingOptions</c>:
    ///         the options extension is public API and says the same thing.
    ///     </para>
    /// </remarks>
    private IReadOnlyList<Common.ServerLogEvent>? CollectedLog(ServerLogCapture.Scope? capture, DbContext context)
    {
        if (capture?.Events is not { } events)
        {
            return null;
        }

        bool sensitive = context.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()
            ?.IsSensitiveDataLoggingEnabled == true;

        return sensitive && !SensitiveLogForwardingAllowed ? null : events;
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
    ///         its own <c>TransactionIgnoredWarning</c> and hands back a stub, and relaying that is
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

            // THE WIRE FORMAT IS UNCHANGED, which is what separates this from #54's part 2. The
            // token has always been an opaque string this server mints and the client echoes back
            // verbatim, so what is INSIDE it is this server's business alone.
            string token = $"{_instanceId}.{Guid.NewGuid():N}";
            var open = new OpenTransaction(scope, context, transaction);
            open.Touch(Clock);
            _transactions[token] = open;

            // BOTH SWEEPS, and this is the inline one. The timer below covers a server that goes
            // quiet, which is the abandoned-client case exactly; this covers the case where the
            // timer has not been armed yet and costs one pass over a dictionary that is normally
            // empty.
            ArmSweepTimer();
            SweepIdle();

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

    /// <summary>
    ///     The instance named by a token, or <see langword="null" /> when it names none.
    /// </summary>
    private static string? MintedBy(string transactionId)
    {
        int separator = transactionId.IndexOf('.', StringComparison.Ordinal);
        return separator > 0 ? transactionId[..separator] : null;
    }

    /// <summary>
    ///     Why a token is not open here, said as precisely as this server can say it.
    /// </summary>
    /// <remarks>
    ///     <b>The routing case is separated from the ended case because they need different
    ///     actions.</b> An ended transaction is the caller's own history and there is nothing to
    ///     configure; a misrouted one is a deployment fact, and the reader has to reach for
    ///     session affinity rather than for their own code. Listing both causes in one sentence,
    ///     which is what this used to do, reliably sent readers to the wrong one.
    /// </remarks>
    private string NotOpenHere(string transactionId, string ended)
    {
        string? mintedBy = MintedBy(transactionId);

        return mintedBy is not null && !string.Equals(mintedBy, _instanceId, StringComparison.Ordinal)
            ? $"Transaction '{transactionId}' was opened on a different server instance "
                + $"('{mintedBy}'), and this instance is '{_instanceId}'. The registry of open "
                + "transactions is per process, so it does not move between instances and a "
                + "load-balanced deployment needs session affinity for the life of a "
                + "transaction. A restarted process is a different instance too."
            : ended;
    }

    private OpenTransaction Open(string transactionId)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        if (!_transactions.TryGetValue(transactionId, out OpenTransaction? open))
        {
            throw new InvalidOperationException(
                NotOpenHere(
                    transactionId,
                    $"Transaction '{transactionId}' is not open on this server. It was committed, "
                        + "rolled back, evicted after its configured idle timeout, or never "
                        + "opened here."));
        }

        // THE ONE PLACE LIVENESS IS REFRESHED, and it is the one place because every request
        // naming a token arrives here: `Acquire` calls it for a query and a save, and the four
        // savepoint operations call it directly. So the idle timeout of #54 measures IDLENESS
        // rather than age, and a long unit of work that keeps talking is never evicted.
        open.Touch(Clock);
        return open;
    }

    private async Task EndAsync(string transactionId, bool commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        if (!_transactions.TryRemove(transactionId, out OpenTransaction? open))
        {
            // A ROLLBACK naming a token this server does not hold is not an error, and the reason
            // is stronger than convention: `InfoCarrierTransaction.DisposeAsync` calls
            // `RollbackAsync` UNCONDITIONALLY, including after a successful commit. So the
            // implicit end of every `using` block is a rollback for a token this server has
            // already removed. Refusing it would make every committed transaction throw on
            // disposal.
            //
            // A COMMIT IS NOT THE SAME, AND TREATING IT THE SAME WAS A HAZARD (#54). Once a server
            // can evict an abandoned transaction it has ALREADY ROLLED THAT WORK BACK, so a commit
            // arriving afterwards is a client asking to commit writes that no longer exist.
            // Returning quietly reports success for them:
            //
            //     Begin -> SaveChanges -> (idle past the timeout: evicted and rolled back)
            //           -> Commit -> "succeeded", and nothing was written.
            //
            // The reasoning that first allowed the silence to stand claimed such a client would
            // meet the refusal at its next request. IT DOES NOT: commit is the one operation that
            // never looks the token up, and the work happened BEFORE the idle period rather than
            // after it. A wrong answer is worse than an exception, so a commit throws.
            if (commit)
            {
                throw new InvalidOperationException(
                    NotOpenHere(
                        transactionId,
                        $"Transaction '{transactionId}' is not open on this server, so it cannot "
                            + "be committed. It was already committed or rolled back, it was "
                            + "never opened here, or this server rolled it back because no "
                            + "request named it for longer than its configured idle timeout. Any "
                            + "work done inside it has been discarded."));
            }

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

    private sealed record OpenTransaction(AsyncServiceScope Scope, DbContext Context, IDbContextTransaction Transaction)
    {
        private long _lastUsedTicks;

        /// <summary>
        ///     Records that a request has just named this transaction.
        /// </summary>
        /// <remarks>
        ///     A plain volatile write of a tick count. There is no lock because there is nothing to
        ///     lose: two concurrent requests both writing "now" leave the entry alive either way,
        ///     which is the only property the sweep reads it for.
        /// </remarks>
        public void Touch(TimeProvider clock)
            => Volatile.Write(ref _lastUsedTicks, clock.GetUtcNow().UtcTicks);

        /// <summary>
        ///     Whether nothing has named this transaction for <paramref name="idleTimeout" />.
        /// </summary>
        public bool IsIdleFor(TimeProvider clock, TimeSpan idleTimeout)
            => clock.GetUtcNow().UtcTicks - Volatile.Read(ref _lastUsedTicks) >= idleTimeout.Ticks;
    }

    /// <summary>
    ///     Starts the sweep timer, once, if a timeout is registered.
    /// </summary>
    private void ArmSweepTimer()
    {
        if (TransactionTimeout is not { } timeout)
        {
            return;
        }

        lock (_lifecycle)
        {
            if (_sweepTimer is not null || _disposed)
            {
                return;
            }

            // A QUARTER OF THE TIMEOUT, so the worst case a transaction outlives its deadline by is
            // a quarter of it rather than all of it. Cheap: the callback walks a dictionary that
            // holds one entry per open transaction, and a server with none does nothing.
            TimeSpan period = timeout.IdleTimeout / 4;

            _sweepTimer = Clock.CreateTimer(_ => SweepIdle(), state: null, period, period);
        }
    }

    /// <summary>
    ///     Rolls back and discards every transaction no client has touched for the configured
    ///     idle timeout (#54).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b><c>TryRemove</c> is the whole race guard, and it already was one.</b> Whoever
    ///         removes an entry owns rolling it back and disposing it. A sweep racing an ordinary
    ///         <c>EndAsync</c> either wins or loses and finds nothing, and either way exactly one
    ///         of them rolls the transaction back.
    ///     </para>
    ///     <para>
    ///         <b>What the loser is told depends on which end it was, and that asymmetry is
    ///         deliberate.</b> A rollback that finds the entry gone returns silently, because the
    ///         implicit end of a <c>using</c> block is a rollback for a token this server may
    ///         already have removed. A COMMIT that finds it gone throws, because this sweep has
    ///         already rolled that work back and reporting success for it would be a wrong answer.
    ///         <c>EndAsync</c> carries the full reading.
    ///     </para>
    ///     <para>
    ///         <b>The rollback is not awaited</b>, because this runs on a timer callback and, when
    ///         called inline, on a caller's request. The removal is what makes the token dead, and
    ///         that part is synchronous.
    ///     </para>
    /// </remarks>
    private void SweepIdle()
    {
        if (_disposed || TransactionTimeout is not { } timeout)
        {
            return;
        }

        TimeProvider clock = Clock;

        foreach (KeyValuePair<string, OpenTransaction> entry in _transactions)
        {
            if (!entry.Value.IsIdleFor(clock, timeout.IdleTimeout)
                || !_transactions.TryRemove(entry.Key, out OpenTransaction? evicted))
            {
                continue;
            }

            LogEviction(evicted, entry.Key, timeout.IdleTimeout);
            _ = DiscardAsync(evicted);
        }
    }

    /// <summary>
    ///     Rolls an evicted transaction back and releases everything it pinned.
    /// </summary>
    /// <remarks>
    ///     Every failure is swallowed deliberately: this runs unobserved, the caller that would
    ///     have cared is gone by definition, and a store that refuses the rollback of a connection
    ///     that is about to be disposed has nothing left to tell anyone.
    /// </remarks>
    private static async Task DiscardAsync(OpenTransaction evicted)
    {
        try
        {
            await evicted.Transaction.RollbackAsync().ConfigureAwait(false);
        }
        catch
        {
            // Intentionally ignored; see the remarks.
        }

        try
        {
            await evicted.Transaction.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await evicted.Scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Says on the server's own log that a transaction was evicted.
    /// </summary>
    /// <remarks>
    ///     <b>The client is told nothing new</b>, by decision: a later request naming the token
    ///     gets the same message it gets for a transaction that was committed or rolled back,
    ///     because that is what happened to it. The distinction lives here, and
    ///     <c>AddInfoCarrierServerLogForwarding</c> carries it to the client where a deployment has
    ///     granted that as well. <b>The token is logged and nothing else</b>: it is a random
    ///     <c>Guid</c> this server minted, so it names no user and no row.
    ///     <para>
    ///         <b>The logger comes from the EVICTED TRANSACTION'S OWN SCOPE, not from the root
    ///         provider</b>, and that is not a stylistic choice. <c>ILoggerFactory</c> can be registered
    ///         scoped, and resolving a scoped service from the root is the defect
    ///         <c>BuildServiceProvider(validateScopes: true)</c> exists to catch. It caught this one.
    ///         The scope is alive here by construction: <c>DiscardAsync</c> disposes it after this
    ///         returns.
    ///     </para>
    /// </remarks>
    private static void LogEviction(OpenTransaction evicted, string token, TimeSpan idleTimeout)
        => evicted.Scope.ServiceProvider.GetService<ILoggerFactory>()
            ?.CreateLogger<InProcessInfoCarrierServer>()
            .LogWarning(
                "InfoCarrier rolled back transaction {TransactionId}: no request named it for {IdleTimeout}. "
                + "The client that began it did not commit, roll back, or dispose. "
                + "See AddInfoCarrierServerTransactionTimeout.",
                token,
                idleTimeout);

    /// <summary>
    ///     Stops the sweep and releases every transaction still open.
    /// </summary>
    /// <remarks>
    ///     <b>Added in 10.2.0, and additive: this class shipped without it.</b> A sealed class
    ///     gaining an interface breaks no consumer, and the documented server registers this as a
    ///     singleton, so the DI container already owns the teardown that now has something to do.
    ///     Rolling the survivors back is the honest end state: the process is going away, and a
    ///     transaction nobody will ever commit should not be left to a connection's finalizer.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        lock (_lifecycle)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _sweepTimer?.Dispose();
        _sweepTimer = null;

        foreach (string token in _transactions.Keys)
        {
            if (_transactions.TryRemove(token, out OpenTransaction? open))
            {
                await DiscardAsync(open).ConfigureAwait(false);
            }
        }
    }

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
