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
public class ServerQueryExecutor(DbContext context, IExpressionSerializer expressionSerializer)
{
    private readonly DbContext _context = context;
    private readonly IExpressionSerializer _expressionSerializer = expressionSerializer;

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
            return MapResults(result, query, request.ReturnsSingleResult, shape);
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
        // Resolve the entity type through the server model — shared-type entities by name
        // (research-findings §3), CLR-type entities by type.
        IEntityType? entityType = stub.ElementType.EntityTypeName is not null
            ? _context.Model.FindEntityType(stub.ElementType.EntityTypeName)
            : _context.Model.FindEntityType(elementType);
        entityType ??= _context.Model.FindEntityType(elementType)
            ?? throw new InvalidOperationException(
                $"Entity type '{stub.ElementType}' not found in the server model.");

        return new Microsoft.EntityFrameworkCore.Query.EntityQueryRootExpression(entityType);
    }

    private async Task<object?> ExecuteQueryAsync(Expression query, QueryDataRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);

        // A single-result query (Single/First/Count/Any/…) has the *result* as its expression
        // type, not a sequence. It must go straight through the provider: wrapping it in
        // EntityQueryable<T> and enumerating is invalid, because EntityQueryable<T> requires
        // an expression of type IQueryable<T>, and EF then fails compiling a scalar body into
        // an IEnumerable-returning executor.
        if (request.ReturnsSingleResult)
        {
            return QueryProvider.Execute(query);
        }

        IQueryable queryable = BuildQueryable(query);

        // **The result is drained first unless it is untracked, and the reason is the change
        // tracker rather than the query** (D7 buffering point 1, corrected by measurement).
        //
        // `SerializeRows`' probes — `IsLoaded`, `IsTracked`, `ReadShadowValue`, `ReadJoinEntities`,
        // `FindEntityType` — all interrogate the server's `IStateManager`, and under any behaviour
        // that resolves identity the state manager is only complete once the query has been
        // enumerated to the end: a later row can add to an earlier row's collection, and a join
        // row's far side can have its inverse navigation marked loaded after the near side has
        // already been written. Pulling rows straight from EF measured **29 broken**, every one of
        // them a skip navigation or an `IsLoaded` assertion in `ManyToMany*`.
        //
        // Under `NoTracking` there is no identity map and no entry to be incomplete: EF returns a
        // fresh instance per row with its includes already materialized, every probe falls through
        // to reading the navigation value, and nothing a later row does can change an earlier one.
        // So that behaviour — which is what a large read-only result set uses, and what the
        // 560 MB results in this suite are — streams, and the rest is drained exactly as before.
        //
        // The larger buffer goes either way: `SerializeRows` writes each row into the response as
        // it maps it rather than building a `List<DynamicValueNode>` first, so the node graph — the
        // biggest of the three copies — is never held at all.
        if (_context.ChangeTracker.QueryTrackingBehavior == QueryTrackingBehavior.NoTracking)
        {
            return queryable;
        }

        var drained = new ArrayList();
        foreach (object? item in queryable)
        {
            drained.Add(item);
        }

        return drained;
    }

    /// <summary>
    ///     The element type a query's rows have: its own result type when it returns a single
    ///     result, and its sequence's element type otherwise.
    /// </summary>
    /// <remarks>
    ///     <b>A method rather than the two-line conditional it replaces, and the reason is the trim
    ///     gate.</b> ILLink reports an unannotated <c>Type</c> flowing into
    ///     <c>IModel.FindEntityType</c> once <em>per origin</em>, so writing the conditional at the
    ///     call site produced two warnings for one call — one naming
    ///     <see cref="GetElementType" />'s return and one naming <c>Expression.Type</c>'s getter —
    ///     and took `eng/trim-baseline.txt` from 88 to 89. Behind one return there is one origin
    ///     and one warning. The warning itself is not removable: which type a caller's query names
    ///     is the premise of this provider, which is what that baseline exists to say.
    /// </remarks>
    private static Type ElementTypeOf(Expression query, bool singleResult)
        => singleResult ? query.Type : GetElementType(query.Type);

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

    /// <summary>
    ///     Describes the result and hands back its rows, without reading one.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The element type is the one the <em>query</em> declares. It used to be the first
    ///         non-null row's runtime type, which a streamed response cannot use: the header goes
    ///         out before a row has been seen. The two differ only where a row is a proxy or a
    ///         derived type, and the declared type is the better answer — a lazy-loading proxy's
    ///         CLR type is not in the model, so the old rule reported
    ///         <c>IsEntityResult: false</c> for a result that was entirely entities. Nothing in
    ///         this provider routes on either member; both are diagnostics.
    ///     </para>
    ///     <para>
    ///         Entity-typed results → identity-keyed rows; projections → columnar data
    ///         (E2 refines).
    ///     </para>
    /// </remarks>
    private QueryDataResult MapResults(object? result, Expression query, bool singleResult, ProjectionShape? shape)
    {
        Type elementType = ElementTypeOf(query, singleResult);

        return new QueryDataResult
        {
            Rows = SerializeRows(ToRows(result, singleResult), elementType, shape),
            IsEntityResult = _context.Model.FindEntityType(elementType) is not null,
            ElementTypeName = elementType.FullName,
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
    private static IEnumerable<object?> ToRows(object? result, bool singleResult)
        => singleResult || result is not IEnumerable enumerable || result is string
            ? [result]
            : enumerable.Cast<object?>();

    /// <summary>
    ///     Serializes result rows as a <see cref="DynamicValueNode" /> graph
    ///     (<c>docs/result-wire-format.md</c>), pulling them one at a time.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Never reflection-serialize the live entity graph: <c>Customer → Orders → Customer</c>
    ///         throws "a possible object cycle was detected", shadow properties are invisible to a
    ///         public-property walk, and lazy-loading proxies get walked as data
    ///         (ADR-008 constraints 1 and 5).
    ///     </para>
    ///     <para>
    ///         <paramref name="rows" /> is enumerated lazily and each row is mapped as it is asked
    ///         for, so the result set is never held as objects <em>and</em> as a
    ///         <c>List&lt;DynamicValueNode&gt;</c> at once (D7 buffering points 1 and 2). Whoever
    ///         enumerates this owns the <c>DbContext</c> it reads through — see
    ///         <see cref="QueryDataResult" />.
    ///     </para>
    ///     <para>
    ///         The mapping itself is synchronous, and so is the <c>foreach</c> over EF's own
    ///         results. This is an <c>IAsyncEnumerable</c> because that is what the wire needs, not
    ///         because the work is asynchronous; pulling rows out of EF through
    ///         <c>IAsyncEnumerable</c> as well is a further step and is not this one.
    ///     </para>
    /// </remarks>
    private async IAsyncEnumerable<DynamicValueNode> SerializeRows(
        IEnumerable<object?> rows,
        Type elementType,
        ProjectionShape? shape)
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

        // A null row is data, not an absent row, and it still needs a type: the declared element
        // type answers that without a row having to be seen, which is what the buffered format
        // needed the whole result set for.
        foreach (object? item in rows)
        {
            yield return item is null
                ? mapper.ToDynamicValue(null, elementType)
                : mapper.ToRowValue(
                    item, item.GetType(), IsLoaded, IsTracked, ReadShadowValue, ReadJoinEntities, FindEntityType, shape);
        }
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
}
