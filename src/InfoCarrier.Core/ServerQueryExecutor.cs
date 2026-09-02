// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core;

/// <summary>
///     Executes a deserialized query against the server's real <see cref="DbContext" />
///     (research-findings §2/§8). Rebinds <see cref="QueryRootStubNode" />s to real
///     <c>EntityQueryRootExpression</c> / <c>DbSet&lt;T&gt;</c> roots via the server model
///     (shared-type entities resolved by name), executes the entity-typed portion, and maps
///     results to the wire format.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="ServerQueryExecutor" /> class.
/// </remarks>
/// <param name="context">The server's context.</param>
/// <param name="expressionSerializer">The serializer built for the server's model.</param>
/// <param name="arbitrarySqlAllowed">
///     Whether this server permits a payload to carry SQL it will execute (#60) - true when the
///     application registered
///     <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierArbitrarySqlExecution" />.
///     <b>This is the security boundary</b>; the client's matching option only decides what it
///     sends. Default <c>false</c>, so a server that says nothing refuses.
/// </param>
public class ServerQueryExecutor(
    DbContext context,
    IExpressionSerializer expressionSerializer,
    bool arbitrarySqlAllowed = false)
{
    private readonly DbContext _context = context;
    private readonly IExpressionSerializer _expressionSerializer = expressionSerializer;
    private readonly bool _arbitrarySqlAllowed = arbitrarySqlAllowed;

    /// <summary>
    ///     Executes the query described by <paramref name="request" /> and returns the wire result.
    /// </summary>
    public virtual async Task<QueryDataResult> ExecuteAsync(QueryDataRequest request, CancellationToken cancellationToken)
    {
        // Start of a message exchange: wire reference ids restart at 1.
        ((DynamicValueMapper)((ExpressionSerializer)_expressionSerializer).ValueMapper).ResetReferenceScope();

        try
        {
            // Deserialize + rebind the tree against the server model.
            ExpressionNode node = DeserializeNode(request.SerializedQuery);
            Expression query = Rebind(node);

            // The query is read for the entity types its rows carry: an owned or shared-type value
            // projected directly has no other name (A56). Read before execution, because it also
            // decides whether this query can be tracked at all.
            ProjectionShape? shape = ProjectionShape.Of(query);

            // Match the client's tracking behavior on the server context.
            _context.ChangeTracker.QueryTrackingBehavior = TrackingBehaviorFor(request.TrackingBehavior, query, shape);

            // Execute against the server context.
            object? result = await ExecuteQueryAsync(query, request, cancellationToken).ConfigureAwait(false);

            // Map results to the wire format (entity rows vs columnar projections).
            return MapResults(result, request.ReturnsSingleResult, shape);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Reflection is the server's own business, never part of the contract. The server
            // pipeline reflects in several places — and so does EF: its non-generic
            // `EntityQueryProvider.Execute(Expression)` is `MakeGenericMethod(…).Invoke(…)`, so
            // every translation failure raised through it arrives wrapped. A caller asserting
            // `InvalidOperationException` got `TargetInvocationException` instead.
            //
            // Rethrow the original with its stack intact rather than `throw ex.InnerException`,
            // which would reset it to this line.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // Unreachable; the compiler cannot see that Throw() does not return.
        }
    }

    /// <summary>
    ///     The tracking behaviour the server can actually honour for a <em>carrier</em> row.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF refuses to track a result that carries an owned entity without its owner —
    ///         <c>CoreStrings.OwnedEntitiesCannotBeTrackedWithoutTheirOwner</c>, raised by
    ///         <c>StructuralTypeMaterializerInjector</c> before a row is read — and it is right:
    ///         an owned dependent has no identity apart from its owner, so an entry for one alone
    ///         cannot be keyed. **A user query that asks for this must still be refused**, and
    ///         three spec tests assert it (B17's <c>Project_json_*_in_tracking_query_fails</c>).
    ///     </para>
    ///     <para>
    ///         A <see cref="TupleCarrier" /> row is not a user query. It is what
    ///         <see cref="ProjectionRewriter" /> generated to carry values back:
    ///         <c>Select(c =&gt; new ContactDto { Names = c.Names.Select(…) })</c> — which EF
    ///         itself translates — ships as <c>Select(c =&gt; new ValueTuple(c.Id, c.Names))</c>,
    ///         an owned collection beside a scalar. The refusal there is aimed at a projection the
    ///         caller never wrote, and the server is not the caller anyway: every row is rebuilt
    ///         from the wire on the client, which tracks what the <em>residual</em> yields.
    ///     </para>
    ///     <para>
    ///         So for a carrier the tracking is dropped and the <em>identity resolution</em> kept.
    ///         That half matters and A55 says why: under <c>TrackAll</c> EF returns one instance
    ///         for two rows naming the same entity, the wire sends the second as a back-reference,
    ///         and the client rebuilds one object. <c>NoTrackingWithIdentityResolution</c> keeps
    ///         exactly that and drops exactly the entries EF will not make.
    ///     </para>
    ///     <para>
    ///         Narrow twice over: only a carrier row, and only when
    ///         <see cref="ProjectionShape" /> — which is partial and reports only what it could
    ///         resolve — actually finds an ownerless owned type.
    ///     </para>
    /// </remarks>
    private static QueryTrackingBehavior TrackingBehaviorFor(
        QueryTrackingBehavior requested, Expression query, ProjectionShape? shape)
    {
        if (requested != QueryTrackingBehavior.TrackAll
            || shape is null
            || !TupleCarrier.IsCarrier(ServerBoundaryAnalyzer.SequenceElementType(query.Type)))
        {
            return requested;
        }

        IEntityType[] carried = [.. shape.EntityTypes()];

        return Array.Exists(
                carried,
                e => e.IsOwned()
                    && e.FindOwnership()?.PrincipalEntityType is { } owner
                    && !Array.Exists(carried, c => c == owner))
            ? QueryTrackingBehavior.NoTrackingWithIdentityResolution
            : requested;
    }

    private Expression Rebind(ExpressionNode node)
    {
        // Rebind query-root stubs to real EntityQueryRootExpression via the server model.
        return ((ExpressionSerializer)_expressionSerializer).ToExpression(node, RebindQueryRoot);
    }

    private Expression RebindQueryRoot(QueryRootStubNode stub, Type elementType)
    {
        // The SCALAR raw-SQL root (#56), and it is answered FIRST because it is the one root with
        // no entity type at all. `Database.SqlQuery<int>` names a CLR type the server's model has
        // never heard of, so the lookup below would throw "not found in the server model" on a
        // query that is perfectly well formed. Same grant as its entity-typed sibling, same
        // default refusal, and the check is here for the same reason: the node is well-formed
        // either way, and what a server withholds is permission to EXECUTE a string it did not
        // write.
        if (stub is SqlQueryRootStubNode sqlQuery)
        {
            RequireArbitrarySql();

            return RelationalQueryRootShape.CreateSqlQueryRoot(
                elementType,
                sqlQuery.Sql,
                ((ExpressionSerializer)_expressionSerializer).ToExpression(sqlQuery.Arguments));
        }

        // Resolve the entity type through the server model — shared-type entities by name
        // (research-findings §3), CLR-type entities by type.
        IEntityType? entityType = stub.ElementType.EntityTypeName is not null
            ? _context.Model.FindEntityType(stub.ElementType.EntityTypeName)
            : _context.Model.FindEntityType(elementType);
        entityType ??= _context.Model.FindEntityType(elementType)
            ?? throw new InvalidOperationException(
                $"Entity type '{stub.ElementType}' not found in the server model.");

        // The raw-SQL root (#60). Refused unless this server registered the grant, and the check
        // is here rather than at the parse: the node is well-formed either way, and what a server
        // withholds is permission to EXECUTE a string it did not write. `docs/security-review.md`
        // section 5a is why the message names arbitrary SQL execution rather than `FromSql`.
        if (stub is FromSqlQueryRootStubNode fromSql)
        {
            RequireArbitrarySql();

            // The arguments are rebuilt as an expression and handed to EF, which binds them as
            // DbParameters. They are never spliced into the text: a value in a string has lost
            // its type, and the round trip is the only reason this node carries a tree at all.
            return RelationalQueryRootShape.CreateFromSqlRoot(
                entityType,
                fromSql.Sql,
                ((ExpressionSerializer)_expressionSerializer).ToExpression(fromSql.Arguments));
        }

        return new Microsoft.EntityFrameworkCore.Query.EntityQueryRootExpression(entityType);
    }

    /// <summary>
    ///     Refuses a payload that carries SQL unless this server registered the grant.
    /// </summary>
    /// <remarks>
    ///     One method for both raw-SQL roots, because the grant is one grant. The message names
    ///     arbitrary SQL execution rather than <c>FromSql</c> for the reason
    ///     <c>docs/security-review.md</c> section 5a gives: R94 measured that one
    ///     <c>CommandText</c> runs every statement in it, so there is no read-only subset to
    ///     describe.
    /// </remarks>
    private void RequireArbitrarySql()
    {
        if (!_arbitrarySqlAllowed)
        {
            throw new InvalidOperationException(
                "This server does not permit a payload to carry SQL it will execute. A "
                + "raw-SQL query (FromSql/FromSqlRaw/FromSqlInterpolated/Database.SqlQuery) "
                + "reached it and was refused. Register it with "
                + "services.AddInfoCarrierArbitrarySqlExecution() only after reading "
                + "docs/security-review.md section 5a: it grants arbitrary SQL execution on "
                + "this server's connection, and the server's own query filters are not "
                + "applied to such a query.");
        }
    }

    private Task<object?> ExecuteQueryAsync(Expression query, QueryDataRequest request, CancellationToken cancellationToken)
    {
        // EVERY PATH HERE IS ASYNC AND TAKES THE TOKEN, and that is the whole point of this
        // method's shape. The token arrives from the transport: `MapInfoCarrier` hands
        // `HttpContext.RequestAborted` to `InfoCarrierEnvelopeServer.DispatchAsync`, which hands
        // it to `IInfoCarrierServer.QueryDataAsync`, which hands it here. It used to be accepted
        // and never read, so a client that cancelled stopped waiting while the server read the
        // store to the end and built an answer nobody would receive. On a wide-area link that is
        // the ordinary case, not the rare one: a user closes a screen.
        //
        // Passing it to EF's async path is what makes the store stop, not merely this loop: EF
        // gives the token to the `DbCommand`, so the provider can cancel the command itself.
        //
        // A single-result query (Single/First/Count/Any/…) has the *result* as its expression
        // type, not a sequence. It must go straight through the provider: wrapping it in
        // EntityQueryable<T> and enumerating is invalid, because EntityQueryable<T> requires
        // an expression of type IQueryable<T>, and EF then fails compiling a scalar body into
        // an IEnumerable-returning executor.
        if (request.ReturnsSingleResult)
        {
            return (Task<object?>)ExecuteSingleMethod
                .MakeGenericMethod(query.Type)
                .Invoke(null, [QueryProvider, query, cancellationToken])!;
        }

        return (Task<object?>)BufferMethod
            .MakeGenericMethod(GetElementType(query.Type))
            .Invoke(null, [BuildQueryable(query), cancellationToken])!;
    }

    // Both are closed over the query's own runtime type, which only the caller's model knows.
    // That is the same premise the rest of this provider rests on, and the same reason the trim
    // analyzer cannot prove either of them.
    private static readonly MethodInfo ExecuteSingleMethod =
        typeof(ServerQueryExecutor).GetMethod(nameof(ExecuteSingle), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo BufferMethod =
        typeof(ServerQueryExecutor).GetMethod(nameof(Buffer), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    ///     Runs a single-result query through EF's async provider so the token reaches the store.
    /// </summary>
    /// <remarks>
    ///     <c>TResult</c> is the query's own result type, and EF's contract for a scalar is that
    ///     the caller asks for <c>Task&lt;TResult&gt;</c>. That is why this asks for
    ///     <c>Task&lt;TResult&gt;</c> and awaits it, rather than asking for <c>TResult</c>.
    /// </remarks>
    private static async Task<object?> ExecuteSingle<TResult>(
        Microsoft.EntityFrameworkCore.Query.IAsyncQueryProvider provider,
        Expression query,
        CancellationToken cancellationToken)
        => await provider.ExecuteAsync<Task<TResult>>(query, cancellationToken).ConfigureAwait(false);

    /// <summary>
    ///     Buffers a sequence query, cancellably.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Buffering is deliberate and this is not the place to remove it.</b> The whole
    ///         result set is materialized before anything is written to the wire, because identity
    ///         resolution on the client needs it and because streaming results is out of scope for
    ///         v10 (wire protocol W4, roadmap M8). What changes here is only that the buffering
    ///         can now be abandoned partway.
    ///     </para>
    /// </remarks>
    private static async Task<object?> Buffer<TElement>(IQueryable source, CancellationToken cancellationToken)
    {
        var results = new ArrayList();

        await foreach (TElement item in ((IAsyncEnumerable<TElement>)source)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            results.Add(item);
        }

        return results;
    }

    private IQueryable BuildQueryable(Expression query)
    {
        // Route the rebound tree through the server context's own query provider so EF
        // translates it against the real provider (SQL Server / InMemory / …).
        Type elementType = GetElementType(query.Type);
        Type queryableType = typeof(Microsoft.EntityFrameworkCore.Query.Internal.EntityQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, QueryProvider, query)!;
    }

    /// <summary>
    ///     The server context's query provider.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         One per context, and the same one every <c>DbSet</c> hands out —
    ///         <c>InternalDbSet&lt;T&gt;</c> builds its queryable from
    ///         <c>context.GetDependencies().QueryProvider</c>, which is this service. So there is
    ///         nothing to derive it from and nothing to look up.
    ///     </para>
    ///     <para>
    ///         This used to reflect <c>DbContext.Set&lt;T&gt;()</c> over the query root's entity
    ///         type, which had two problems. Deriving from the query's <em>result</em> type was
    ///         the first and was already fixed by rooting it: a projection
    ///         (<c>Select(c =&gt; new { … })</c>) has a result type the model knows nothing about.
    ///         The second outlived that fix — <c>Set&lt;T&gt;()</c> cannot name a shared-type
    ///         entity at all, so a query rooted at a many-to-many join entity died with "cannot
    ///         create a DbSet for 'Dictionary&lt;string, object&gt;' … access the entity type via
    ///         the 'Set' method overload that accepts an entity type name".
    ///     </para>
    /// </remarks>
    private IQueryProvider QueryProvider
        => _context.GetService<Microsoft.EntityFrameworkCore.Query.IAsyncQueryProvider>();

    private static Type GetElementType(Type queryType)
    {
        // Find the IQueryable<T> interface (handles IOrderedQueryable<T>, EntityQueryable<T>, …).
        Type? queryable = queryType.IsGenericType && queryType.GetGenericTypeDefinition() == typeof(IQueryable<>)
            ? queryType
            : queryType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));
        return queryable?.GetGenericArguments()[0] ?? queryType;
    }

    private QueryDataResult MapResults(object? result, bool singleResult, ProjectionShape? shape)
    {
        List<object?> rows = ToRows(result, singleResult);

        // Entity-typed results → identity-keyed rows; projections → columnar data (E2 refines).
        Type? elementType = rows.FirstOrDefault(r => r is not null)?.GetType();
        bool isEntityResult = elementType is not null
            && _context.Model.FindEntityType(elementType) is not null;

        return new QueryDataResult
        {
            SerializedResults = SerializeResult(rows, elementType, shape),
            IsEntityResult = isEntityResult,
            ElementTypeName = elementType?.FullName,
        };
    }

    /// <summary>
    ///     Splits an execution result into wire rows.
    /// </summary>
    /// <remarks>
    ///     A single result is <em>one</em> row even when it is itself a sequence:
    ///     <c>…Select(c =&gt; c.Orders.Select(o =&gt; o.OrderID)).First()</c> must arrive as one
    ///     list, not as N integer rows. Enumerating unconditionally flattened exactly that away,
    ///     and the client then failed casting an <c>Int32</c> to <c>IEnumerable&lt;int&gt;</c>.
    /// </remarks>
    private static List<object?> ToRows(object? result, bool singleResult)
        => singleResult || result is not IEnumerable enumerable || result is string
            ? [result]
            : enumerable.Cast<object?>().ToList();

    /// <summary>
    ///     Serializes result rows as a <see cref="DynamicValueNode" /> graph
    ///     (<c>docs/result-wire-format.md</c>).
    /// </summary>
    /// <remarks>
    ///     Never reflection-serialize the live entity graph: <c>Customer → Orders → Customer</c>
    ///     throws "a possible object cycle was detected", shadow properties are invisible to a
    ///     public-property walk, and lazy-loading proxies get walked as data
    ///     (ADR-008 constraints 1 and 5).
    /// </remarks>
    private byte[] SerializeResult(List<object?> rows, Type? elementType, ProjectionShape? shape)
    {
        var mapper = (DynamicValueMapper)((ExpressionSerializer)_expressionSerializer).ValueMapper;
        var stateManager = _context.GetService<Microsoft.EntityFrameworkCore.ChangeTracking.Internal.IStateManager>();

        // A no-tracking result is not in the change tracker, and `DbContext.Entry` on an
        // untracked entity does not report on it — it creates a fresh Detached entry, which
        // answers "not loaded" for every navigation (and starts tracking the entity as a side
        // effect). Every Include on a no-tracking query was therefore dropped from the wire and
        // arrived null on the client.
        //
        // For an untracked entity the navigation value has to answer instead. Non-null alone is
        // not enough: Northwind's `Order.Customer` is initialized to `new()` in its field
        // initializer — deliberately, as the regression test for EF issue #23851 — so an Order
        // that never loaded its principal still hands back a Customer. Requiring a key as well
        // separates the two: an entity materialized from the store always has one, and the
        // placeholder never does. A collection needs no such check; EF only ever assigns one
        // when it populates it, empty included.
        // An owned dependent is the one thing the tracker can be wrong about, and only in one
        // direction. "Loaded" is a flag EF sets when something *does the loading*, and nothing
        // loads an owned collection stored inside its owner's row: EF's JSON materializer builds
        // `JsonOwnedRoot.OwnedCollectionBranch` straight out of the document and never flags it, so
        // a tracked entry reports `IsLoaded: false` for a collection it is holding two elements of.
        // Every JSON-mapped owned collection was therefore dropped from the wire and arrived null,
        // which is what 48 of `JsonQuery`'s failures were (B10).
        //
        // Owned *references* were flagged and did travel, which is why only half of each document
        // was missing — and why the answer is not to distrust the tracker generally, but to fall
        // through to the value for the case where "loaded" was never a question. An owned
        // dependent cannot be loaded later: it came with the row or it does not exist.
        static bool IsOwnership(INavigationBase navigation)
            => navigation is INavigation { ForeignKey.IsOwnership: true };

        bool IsLoaded(object entity, INavigationBase navigation)
        {
            if (stateManager.TryGetEntry(entity) is { } entry
                && (entry.IsLoaded(navigation) || !IsOwnership(navigation)))
            {
                return entry.IsLoaded(navigation);
            }

            // A shadow navigation has no CLR member, and `GetGetter` on one throws rather than
            // returning null: "no backing field could be found ... and the property does not have
            // a getter". A *unidirectional* many-to-many is where they come from — EF declares
            // the inverse skip navigation with no property behind it. This is the same defect
            // L18 fixed in `DynamicValueMapper.MapRowMembers`, at the second site; nothing
            // adopted at the time reached this one. An untracked entity has nowhere for the value
            // to be, so the answer is "not loaded" rather than an exception.
            if (navigation.IsShadowProperty())
            {
                return false;
            }

            // Read through `GetGetter()`, deliberately, including for a collection — even though
            // that getter is typed for the navigation's *target* and throws
            // `InvalidCastException: Unable to cast HashSet<Order> to Order` on an owned
            // collection whose declaring type is a base of the instance's (A51). Reading the
            // backing field instead is **measured worse**: it bypasses a lazy-loading proxy, and
            // the suite answered with 102 `Assert.False()` failures and a family of "the
            // navigation cannot have 'IsLoaded' set to false" across the lazy bases. Whatever
            // fixes the cast has to keep going through the model's own accessor.
            object? related = navigation.GetGetter().GetClrValue(entity);

            return related is not null
                && (navigation.IsCollection || HasKey(related, navigation.TargetEntityType));
        }

        // An entity constructed by a projection is not in the change tracker; the client needs
        // to know so it does not identity-resolve rows that all carry a default key.
        bool IsTracked(object entity)
            => stateManager.TryGetEntry(entity) is not null;

        // The entity type of an instance whose CLR type cannot name one — a shared-type entity,
        // where several entity types are the same `Dictionary<string, object>`. The tracker is
        // the only thing that knows which, and it answers null for anything that is not an
        // entity at all, which is most of what passes through here.
        IEntityType? FindEntityType(object entity)
            => stateManager.TryGetEntry(entity)?.EntityType;

        // A shadow property — a TPH discriminator, an unmapped foreign key — has no CLR member,
        // so its value lives in the entry. `GetGetter()` on one does not return null, it throws.
        // An untracked entity has no entry and therefore no shadow state to report.
        object? ReadShadowValue(object entity, Microsoft.EntityFrameworkCore.Metadata.IProperty property)
            => stateManager.TryGetEntry(entity) is { } entry ? entry.GetCurrentValue(property) : null;

        // The join rows behind a loaded skip navigation. Found by scanning the tracked entries
        // of the join type for the ones whose foreign key points at this entity: there is no
        // navigation from a principal to its join rows to read instead — `EntityOne` declares
        // only `TwoSkip` — and EF materialized them to build that collection, so they are here.
        // The entity a join row points at through <paramref name="inverse" /> — the other end of
        // the many-to-many — or null if it is not tracked here.
        object? FarSide(IUpdateEntry joinRow, ISkipNavigation inverse)
        {
            IForeignKey foreignKey = inverse.ForeignKey;
            object?[] keyValues = [.. foreignKey.Properties.Select(joinRow.GetCurrentValue)];

            return Array.Exists(keyValues, v => v is null)
                ? null
                : stateManager.TryGetEntry(foreignKey.PrincipalKey, keyValues)?.Entity;
        }

        IEnumerable<object> ReadJoinEntities(object entity, ISkipNavigation skip, bool loaded)
        {
            if (stateManager.TryGetEntry(entity) is not { } ownerEntry)
            {
                yield break;
            }

            IForeignKey foreignKey = skip.ForeignKey;
            object?[] ownerKey = [.. foreignKey.PrincipalKey.Properties.Select(ownerEntry.GetCurrentValue)];

            foreach (IUpdateEntry candidate in stateManager.Entries)
            {
                if (candidate.EntityType != skip.JoinEntityType)
                {
                    continue;
                }

                bool matches = true;
                for (int i = 0; i < foreignKey.Properties.Count; i++)
                {
                    if (!Equals(candidate.GetCurrentValue(foreignKey.Properties[i]), ownerKey[i]))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                // Both ends of a many-to-many name the same join rows, and exactly one of them
                // should send each. The side whose navigation EF actually *loaded* owns them: it
                // sends the whole set in one run, which is the order the client then rebuilds the
                // navigation in. Letting the other end send them too interleaved them one per
                // entity, and a `List`-backed navigation came out in a different order — the 18
                // tests A6 broke, all `Load_collection_using_Query_already_loaded*`.
                //
                // When *neither* side is loaded — which is the case A6 exists for, an explicit
                // load whose include EF deliberately leaves unloaded — this yields, and the join
                // rows travel from here.
                if (!loaded
                    && skip.Inverse is { } inverse
                    && FarSide(candidate, inverse) is { } far
                    && IsLoaded(far, inverse))
                {
                    continue;
                }

                yield return candidate.ToEntityEntry().Entity;
            }
        }

        // A *shadow* key property is skipped rather than read. `GetGetter` on one throws — the
        // third site of the defect L18 and A3 fixed, reached first by an **owned** entity type,
        // whose key is its owner's foreign key and has no CLR member at all
        // ("No backing field could be found for property
        // 'BaseInheritanceRelationshipEntity.OwnedReferenceOnBase#OwnedEntity.…Id'"). The question
        // this answers is whether the instance came from the store or is a constructor-set
        // placeholder, and only a CLR-visible key can distinguish those; a key that is entirely
        // shadow leaves nothing to check, which reads as "from the store".
        static bool HasKey(object entity, IEntityType entityType)
            => entityType.FindPrimaryKey() is not { } key
                || key.Properties.All(p => p.IsShadowProperty() || p.GetGetter().GetClrValue(entity) is not null);

        var nodes = new List<DynamicValueNode>();
        foreach (object? item in rows)
        {
            nodes.Add(item is null
                ? mapper.ToDynamicValue(null, elementType ?? typeof(object))
                : mapper.ToRowValue(
                    item, item.GetType(), IsLoaded, IsTracked, ReadShadowValue, ReadJoinEntities, FindEntityType, shape));
        }

        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            nodes, ExpressionJsonContext.Default.ListDynamicValueNode);
    }

    /// <summary>
    ///     The query tree as it arrives from a remote client — the one deserialization on the
    ///     server that is entirely caller-controlled, and so the one the size bound most needs to
    ///     be on (milestone M5).
    /// </summary>
    /// <remarks>
    ///     It uses <see cref="InfoCarrierPayloadLimits.Default" /> rather than a configured
    ///     instance because this executor is constructed from <c>(DbContext, IExpressionSerializer)</c>
    ///     and has no options seam to read one from. That is the same gap M5's envelope criterion
    ///     names: the expression payload travels through <see cref="ExpressionJsonContext" />
    ///     directly rather than through the configured <see cref="IInfoCarrierSerializer" />, so
    ///     nothing configured on the latter reaches here yet. The bound is applied at the default
    ///     in the meantime, because an unconfigurable bound is worth more than no bound.
    /// </remarks>
    private static ExpressionNode DeserializeNode(byte[] payload)
    {
        InfoCarrierPayloadLimits.Default.GuardRequest(payload.Length, "serialized query");
        return System.Text.Json.JsonSerializer.Deserialize<ExpressionNode>(
            payload, ExpressionJsonContext.Default.ExpressionNode)!;
    }
    /// <summary>
    ///     Creates an executor that refuses a payload carrying raw SQL.
    /// </summary>
    /// <remarks>
    ///     <b>A binary-compatibility overload, kept because 10.0.0 shipped this signature.</b>
    ///     Adding an optional parameter is source-compatible and <em>binary</em> breaking: the
    ///     compiler emits one member and the old arity disappears from the assembly, which
    ///     <c>dotnet pack</c>'s package validation reports as <c>CP0002</c>.
    ///     <c>Directory.Build.props</c> states the promise this keeps, and the <c>Packages</c>
    ///     workflow is the only job that checks it — it runs on <c>main</c> alone, which is how
    ///     six of these reached <c>main</c> unnoticed. Delete when the baseline moves past 10.0.x.
    /// </remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public ServerQueryExecutor(DbContext context, IExpressionSerializer expressionSerializer)
        : this(context, expressionSerializer, arbitrarySqlAllowed: false)
    {
    }

}
