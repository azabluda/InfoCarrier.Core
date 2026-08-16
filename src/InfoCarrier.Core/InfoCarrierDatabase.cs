// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace InfoCarrier.Core;

/// <summary>
///     The InfoCarrier client provider's <see cref="IDatabase" />: the raw-capture query entry
///     point (ADR-006). <see cref="CompileQuery{TResult}" /> intercepts the LINQ tree before
///     EF's translation pipeline, substitutes compiled-query parameters as plain constants
///     (research-findings §6), serializes the tree, and ships it to the server. SaveChanges
///     lands in Step 10.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="InfoCarrierDatabase" /> class.
/// </remarks>
public class InfoCarrierDatabase(
    IDbContextOptions options,
    IExpressionSerializer expressionSerializer,
    ICurrentDbContext currentContext,
    IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger,
    IDiagnosticsLogger<DbLoggerCategory.Query> queryLogger) : IDatabase
{
    private static readonly System.Reflection.MethodInfo ExecuteQueryMethod
        = typeof(InfoCarrierDatabase).GetMethod(nameof(ExecuteQuery), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private readonly IInfoCarrierClient _client = options.Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .First()
            .InfoCarrierClient!;
    private readonly IExpressionSerializer _expressionSerializer = expressionSerializer;
    private readonly ICurrentDbContext _currentContext = currentContext;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Update> _updateLogger = updateLogger;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Query> _queryLogger = queryLogger;

    /// <summary>
    ///     The client used to ship operations to the server.
    /// </summary>
    protected virtual IInfoCarrierClient Client => _client;

    /// <inheritdoc />
    public virtual Func<QueryContext, TResult> CompileQuery<TResult>(Expression query, bool async)
    {
        // Every other provider raises this from `QueryCompilationContext.CreateQueryExecutorExpression`,
        // which this provider never reaches — the whole point of ADR-006 is to take the tree
        // *before* EF's translation pipeline. So the event, and with it every
        // `IQueryExpressionInterceptor`, has to be raised from the capture point instead. Not
        // optional and not only a log line: the interceptor's return value is the query, which is
        // what `Intercept_to_change_query_expression` asserts (A67 found it never ran at all).
        //
        // Raised before the shape questions below, so an interceptor that replaces the tree is
        // answered about *its* tree and not the caller's.
        var expressionPrinter = new ExpressionPrinter();
        query = _queryLogger.QueryCompilationStarting(
            _currentContext.Context, expressionPrinter, query).Query;

        // The other half of the pair, and it is observable: a diagnostic listener asserts the two
        // events arrive in this order. EF raises it from the `finally` of the same method, over the
        // executor expression it built; there is no executor expression here, and the query itself
        // is the honest answer to "what was planned" — this provider's plan *is* the tree it is
        // about to ship.
        _queryLogger.QueryExecutionPlanned(_currentContext.Context, expressionPrinter, query);

        // Determine the element type and whether the query returns a single result.
        Type resultType = typeof(TResult);
        bool singleResult = QueryReturnsSingleResult(query);
        Type elementType = singleResult
            ? (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>)
                ? resultType.GetGenericArguments()[0]
                : resultType)
            : (resultType == typeof(IEnumerable<object>) || !resultType.IsGenericType
                ? typeof(object)
                : resultType.GetGenericArguments()[0]);

        var executeQuery = ExecuteQueryMethod
            .MakeGenericMethod(elementType)
            .CreateDelegate<Func<QueryContext, Expression, IInfoCarrierClient, IExpressionSerializer, bool, bool, object>>();

        // The client comes from the context being queried, not from this instance. EF caches
        // what `CompileQuery` returns in `ICompiledQueryCache`, a singleton of the *internal*
        // service provider — and that provider is shared by every context with the same options
        // shape, exactly as it is for EF's own providers, whose `GetServiceProviderHashCode` is
        // likewise blind to which store they talk to. Capturing `_client` here therefore pinned
        // the cached delegate to whichever context compiled the query first, and every later
        // context running the same query shipped it to *that* server while its SaveChanges went
        // to its own. Two contexts against two servers is the ordinary case this provider exists
        // for; it showed up as one concurrency test reading another's data.
        return queryContext => (TResult)executeQuery(
            queryContext, query, ClientFor(queryContext), _expressionSerializer, async, singleResult);
    }

    /// <summary>
    ///     The server's token for the transaction open on a context, or <see langword="null" />
    ///     if none is (wire-protocol W3).
    /// </summary>
    /// <remarks>
    ///     Read from the context rather than held here for the same reason
    ///     <see cref="ClientFor" /> is: what <see cref="CompileQuery{TResult}" /> returns is
    ///     cached across every context sharing an options shape, so anything per-context has to
    ///     be resolved per execution.
    /// </remarks>
    internal static string? ServerTransactionId(DbContext context)
        => (context.GetService<IDbContextTransactionManager>().CurrentTransaction
                as InfoCarrierTransaction)?.ServerTransactionId;

    /// <summary>
    ///     The <see cref="IInfoCarrierClient" /> configured on the context a query is running
    ///     against.
    /// </summary>
    private static IInfoCarrierClient ClientFor(QueryContext queryContext)
        => queryContext.Context.GetService<IDbContextOptions>()
            .Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .First()
            .InfoCarrierClient!;

    private static object ExecuteQuery<TElement>(
        QueryContext queryContext,
        Expression query,
        IInfoCarrierClient client,
        IExpressionSerializer expressionSerializer,
        bool async,
        bool singleResult)
        => new QueryExecutor<TElement>(queryContext, query, client, expressionSerializer)
            .Execute(async, singleResult);

    private static bool QueryReturnsSingleResult(Expression query)
    {
        Type type = query.Type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            type = type.GetGenericArguments()[0];
        }

        // A sequence query is typed IQueryable<T>; everything else is one result. Testing
        // IEnumerable instead conflated the two whenever the single result was itself a
        // sequence — `…Take(1).Select(c => c.Orders.Select(o => o.OrderID)).First()` is typed
        // IEnumerable<int> — so the async path handed EF an IAsyncEnumerable where it wanted a
        // Task<IEnumerable<int>>, and the cast failed. (`string` needed a special case under
        // the old test and needs none under this one.)
        return !typeof(IQueryable).IsAssignableFrom(type);
    }

    /// <inheritdoc />
    public virtual Expression<Func<QueryContext, TResult>> CompileQueryExpression<TResult>(Expression query, bool async)
        => throw new NotImplementedException("Precompiled queries are not supported by InfoCarrier.");

    /// <inheritdoc />
    public virtual int SaveChanges(IList<IUpdateEntry> entries)
        => SaveChangesAsync(entries).GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual async Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var mapper = (Expressions.DynamicValueMapper)((ExpressionSerializer)_expressionSerializer).ValueMapper;
        mapper.ResetReferenceScope();

        // Correlation ids are positions in *this* list, so the expansion has to be materialized
        // once and reused when the generated values come back.
        List<IUpdateEntry> sent = [.. Expand(entries)];

        var request = new Common.SaveChangesRequest
        {
            Entries = [.. sent.Select((e, i) => ChangeEntryMapper.ToChangeEntry(e, i, mapper))],
            TransactionId = ServerTransactionId(_currentContext.Context),
        };


        Common.SaveChangesResult result;
        try
        {
            result = await _client
                .SaveChangesAsync(request, _currentContext.Context, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // A concurrency conflict is detected by the *store*, which is on the far side, so the
            // server's logger recorded it and this context's never did — and
            // `OptimisticConcurrencyTestBase` asserts on the client's:
            // `Fixture.ListLoggerFactory.Log.Single(l => l.Id == CoreEventId.OptimisticConcurrencyException)`.
            //
            // Logging it here is what every other provider does from the same position: EF's
            // InMemory provider from `InMemoryTable`, the relational ones from
            // `AffectedCountModificationCommandBatch`. This provider *is* the store as far as the
            // client context is concerned.
            IReadOnlyList<IUpdateEntry> conflicting = Translate(exception.Entries, sent);

            if ((await _updateLogger.OptimisticConcurrencyExceptionAsync(
                    _currentContext.Context, conflicting, exception, null, cancellationToken)
                .ConfigureAwait(false)).IsSuppressed)
            {
                // An interceptor asked us not to throw. EF's own providers answer that by
                // completing the write anyway; there is nothing to complete here, because the
                // server has already refused the batch. Zero rows is what actually happened.
                return 0;
            }

            // Re-raised rather than rethrown, because `Entries` is the part callers use and the
            // server's entries are useless here: they belong to the server's context, which is
            // disposed with the request scope. Rethrowing gave "cannot access a disposed context
            // instance" the moment `OptimisticConcurrencyTestBase`'s resolver touched
            // `ex.Entries` — `GetDatabaseValues`, `SetValues`, `Reload` all do.
            throw new DbUpdateConcurrencyException(exception.Message, exception, conflicting);
        }
        catch (DbUpdateException exception)
        {
            // The same re-raise, one level up the hierarchy, and W5 is what made it necessary.
            // `DbUpdateException.Entries` is the part callers use — the spec base does
            // `ex.Entries.Single()` and asserts the entity's type — and those entries are *update
            // entries of the server's context*. They cannot cross a wire under any encoding, and
            // in-process they only arrived because the exception was the same object.
            //
            // So the exception is re-raised naming the entries *this* context sent. `Translate`
            // matches the server's entries by key where it has them and falls back to the whole
            // sent list where it does not, which after a wire crossing is always. That is the
            // honest answer: the client knows exactly which of its own entries it submitted, and
            // it is those the caller can do anything with.
            throw new DbUpdateException(exception.Message, exception, Translate([], sent));
        }

        ApplyGeneratedValues(sent, result, mapper);
        return result.Count;
    }

    /// <summary>
    ///     The entries to send: what EF asked to save, plus the <em>deleted</em> half of any row
    ///     being replaced in place.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When an `Added` entry and a `Deleted` one share a key — a collection replaced by
    ///         instances carrying the keys it already held — EF pairs them through
    ///         <see cref="IUpdateEntry.SharedIdentityEntry" /> and hands `IDatabase` only the
    ///         `Added` half. The store is expected to notice the pair and turn it into a replace:
    ///         EF's InMemory provider deletes `SharedIdentityEntry`'s row before creating.
    ///     </para>
    ///     <para>
    ///         That pairing is change-tracker state, and none of it reaches the wire — the server
    ///         saw a bare `Added` for a key the store already had and threw "an item with the same
    ///         key has already been added". Sending the deleted half restores it *without* a wire
    ///         concept for it: the server tracks `Deleted` before `Added` (its replay is ordered
    ///         that way already), and its own state manager re-pairs them on the key.
    ///     </para>
    /// </remarks>
    private static IEnumerable<IUpdateEntry> Expand(IList<IUpdateEntry> entries)
    {
        var seen = new HashSet<IUpdateEntry>();
        var sent = new HashSet<IUpdateEntry>(entries);

        foreach (IUpdateEntry entry in entries)
        {
            if (entry.SharedIdentityEntry is { } replaced && seen.Add(replaced))
            {
                yield return replaced;
            }
        }

        // The owner of a JSON-mapped entry, and every owner above it. EF writes a JSON column by
        // *partial update of the owning row*: `ModificationCommand.FindJsonPartialUpdateInfo`
        // walks from the changed dependent up to the entry that holds the column, and builds the
        // `UPDATE` from it. That entry is normally `Unchanged`, so it is not in `entries` and
        // never travelled — and the server, replaying a change set that contained only the
        // dependent, gave `NullReferenceException` from inside EF's own `ModificationCommand`
        // (C86). Four tests failed on it, and the one that *passed* named the cause: adding a
        // whole new entity works, because there the owner is `Added` and therefore already sent.
        //
        // Ownership order, principal first, which is the order the server's replay wants anyway.
        //
        // Restricted to JSON-mapped types on purpose. Every owned type has an owner, and widening
        // this to all of them would put table-splitting dependents' owners on the wire for no
        // reason — `SaveChanges` payload changes are the ones this provider has paid most for
        // (C37, C42).
        // …and, once the owner is going, the rest of its JSON document (C95).
        //
        // C87 sent the owner and stopped there, which was enough for an *edit* and wrong for an
        // add or a remove. `__synthesizedOrdinal` is **positional**: EF numbers a JSON array from
        // the owner's collection navigation, and the server's owner arrives bare, because
        // `ChangeEntryMapper` sends no navigations. So a document that holds two elements looked
        // like a document that holds one, and the element appended to it came back numbered `1`
        // instead of `3` — a value the client already had, which is why the client's own change
        // tracker raised *"another instance with the key value '{OwnerId: 1,
        // __synthesizedOrdinal: 1}' is already being tracked"*. The identity conflict was the
        // symptom; **the wrong ordinal written into the document was the defect**, and it would
        // have overwritten an existing element rather than appending.
        //
        // The siblings go as `Unchanged`, which is what they are. This is not the general
        // "send the whole graph" that C37 and C42 each paid for: a JSON column is written by
        // partial update of one document, and the other elements of that document are part of the
        // row being written. The scan is bounded by `GetContainerColumnName()` — nothing happens
        // at all on a model with no `ToJson()`.
        foreach (IUpdateEntry entry in entries)
        {
            foreach (IUpdateEntry owner in JsonColumnOwners(entry))
            {
                if (!sent.Contains(owner) && seen.Add(owner))
                {
                    yield return owner;
                }

                foreach (IUpdateEntry sibling in JsonDocumentMembers(owner))
                {
                    if (!sent.Contains(sibling) && seen.Add(sibling))
                    {
                        yield return sibling;
                    }
                }
            }
        }

        foreach (IUpdateEntry entry in entries)
        {
            yield return entry;
        }
    }

    /// <summary>
    ///     Every tracked JSON-mapped entry that lives in <paramref name="owner" />'s document.
    /// </summary>
    /// <remarks>
    ///     Read off the client's change tracker rather than off the navigations, because a
    ///     removed element is no longer in its owner's collection and still has to travel — that
    ///     is the whole of the remove case. An element the client never materialized is not here
    ///     and cannot be: the client can only send what it knows.
    /// </remarks>
    private static IEnumerable<IUpdateEntry> JsonDocumentMembers(IUpdateEntry owner)
    {
        if (owner is not InternalEntityEntry ownerEntry)
        {
            yield break;
        }

        foreach (InternalEntityEntry candidate in ownerEntry.StateManager.Entries)
        {
            if (!ReferenceEquals(candidate, ownerEntry)
                && candidate.EntityType.GetContainerColumnName() is not null
                && JsonColumnOwners(candidate).Any(o => ReferenceEquals(o, ownerEntry)))
            {
                yield return candidate;
            }
        }
    }

    /// <summary>
    ///     The chain of owners above a JSON-mapped entry, nearest first, or nothing at all if the
    ///     entry is not JSON-mapped.
    /// </summary>
    /// <remarks>
    ///     <c>GetContainerColumnName()</c> is the same question
    ///     <see cref="InfoCarrierKeyDiscoveryConvention" /> asks, and it walks the ownership chain
    ///     itself, so a nested owned type inherits the container from whichever ancestor declared
    ///     it. Reading it costs nothing on a model with no <c>ToJson()</c>: it is null and this
    ///     yields immediately.
    /// </remarks>
    private static IEnumerable<IUpdateEntry> JsonColumnOwners(IUpdateEntry entry)
    {
        if (entry.EntityType.GetContainerColumnName() is null)
        {
            yield break;
        }

        var current = entry as InternalEntityEntry;

        while (current?.EntityType.FindOwnership() is { } ownership)
        {
            if (current.StateManager.FindPrincipal(current, ownership) is not { } owner)
            {
                yield break;
            }

            yield return owner;
            current = owner;
        }
    }

    /// <summary>
    ///     The client's entries for the rows the server reported in conflict.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Matched on entity type name and primary key, which is all the two sides share — the
    ///         server's entries are different objects in a different change tracker. A server entry
    ///         with no counterpart is dropped rather than guessed at; if that leaves nothing, the
    ///         whole batch stands in, which is what EF's own providers report when they cannot
    ///         narrow it either.
    ///     </para>
    ///     <para>
    ///         The server's key values are read off the <em>entity instance</em>, never through its
    ///         entry. By the time this runs the exception has unwound the request scope, so the
    ///         server's context is disposed and every entry API on it throws — which is the very
    ///         failure this method exists to stop reaching the caller. A shadow key has no
    ///         instance to read and simply does not match.
    ///     </para>
    /// </remarks>
    private static IReadOnlyList<IUpdateEntry> Translate(
        IReadOnlyList<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> serverEntries,
        List<IUpdateEntry> sent)
    {
        var matched = new List<IUpdateEntry>();

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry serverEntry in serverEntries)
        {
            foreach (IUpdateEntry candidate in sent)
            {
                if (candidate.EntityType.Name == serverEntry.Metadata.Name
                    && !matched.Contains(candidate)
                    && KeysMatch(candidate, serverEntry))
                {
                    matched.Add(candidate);
                    break;
                }
            }
        }

        return matched.Count > 0 ? matched : sent;
    }

    private static bool KeysMatch(
        IUpdateEntry candidate,
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry serverEntry)
    {
        if (candidate.EntityType.FindPrimaryKey() is not { } key)
        {
            return false;
        }

        foreach (Microsoft.EntityFrameworkCore.Metadata.IProperty property in key.Properties)
        {
            if (property.IsShadowProperty()
                || !Equals(
                    candidate.GetCurrentValue(property),
                    property.GetGetter().GetClrValue(serverEntry.Entity)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Writes store-generated values back onto the client's entries.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Keyed by correlation id rather than by key value, because the whole point is that the
    ///         client's key was temporary and the server's is not (research-findings §9). Setting the
    ///         current value is what lets EF's own <c>AcceptAllChanges</c> replace the temporary key
    ///         and fix up everything that referenced it.
    ///     </para>
    ///     <para>
    ///         <b>Which properties may receive a generated value is decided twice</b>, and the two
    ///         answers differ — the same asymmetry CLAUDE.md states for type mappings, one level up.
    ///         <c>ValueGenerated</c> is not stated by the model builder; it is inferred by a
    ///         convention, and the one that reads <c>HasDefaultValue</c> is
    ///         <c>RelationalValueGenerationConvention</c>, which the server's provider runs and this
    ///         one does not. So a property the server generates is <c>ValueGenerated.Never</c> on the
    ///         client, has no store-generated slot, and <c>SetStoreGeneratedValue</c> refuses it
    ///         (B6, 8 failures in <c>StoreGenerated</c>).
    ///     </para>
    ///     <para>
    ///         The client therefore accepts whatever the server actually generated rather than
    ///         deciding in advance what it may generate: with no slot to write to, the value is
    ///         written as the current value instead. Nothing is lost — the sidecar exists so that
    ///         <c>AcceptAllChanges</c> can promote a generated value over an explicit one, and a
    ///         property the client believes is never generated has no such conflict to resolve.
    ///     </para>
    /// </remarks>
    private static void ApplyGeneratedValues(
        List<IUpdateEntry> entries,
        Common.SaveChangesResult result,
        Expressions.DynamicValueMapper mapper)
    {
        foreach (Common.GeneratedValues generated in result.GeneratedValues)
        {
            if (generated.CorrelationId < 0 || generated.CorrelationId >= entries.Count)
            {
                continue;
            }

            IUpdateEntry entry = entries[generated.CorrelationId];
            var entityType = (Microsoft.EntityFrameworkCore.Metadata.IEntityType)entry.EntityType;

            foreach (Expressions.DynamicPropertyValue value in ChangeEntryMapper.ReadValues(generated.SerializedValues))
            {
                if (entityType.FindProperty(value.Name) is { } property)
                {
                    object? clientValue = Expressions.PrimitiveCoercion.FromWireValue(
                        property, mapper.FromPropertyValue(value, Expressions.PrimitiveCoercion.WireType(property)));

                    if (Microsoft.EntityFrameworkCore.Metadata.Internal.PropertyBaseExtensions
                        .GetStoreGeneratedIndex(property) == -1)
                    {
                        entry.ToEntityEntry().Property(property.Name).CurrentValue = clientValue;
                    }
                    else
                    {
                        entry.SetStoreGeneratedValue(property, clientValue);
                    }
                }
            }
        }
    }
}
