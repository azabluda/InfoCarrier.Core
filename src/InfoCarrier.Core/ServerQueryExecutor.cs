// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
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
public class ServerQueryExecutor
{
    private readonly DbContext _context;
    private readonly IExpressionSerializer _expressionSerializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServerQueryExecutor" /> class.
    /// </summary>
    public ServerQueryExecutor(DbContext context, IExpressionSerializer expressionSerializer)
    {
        _context = context;
        _expressionSerializer = expressionSerializer;
    }

    /// <summary>
    ///     Executes the query described by <paramref name="request" /> and returns the wire result.
    /// </summary>
    public virtual async Task<QueryDataResult> ExecuteAsync(QueryDataRequest request, CancellationToken cancellationToken)
    {
        // Match the client's tracking behavior on the server context.
        _context.ChangeTracker.QueryTrackingBehavior = request.TrackingBehavior;

        // Start of a message exchange: wire reference ids restart at 1.
        ((DynamicValueMapper)((ExpressionSerializer)_expressionSerializer).ValueMapper).ResetReferenceScope();

        try
        {
            // Deserialize + rebind the tree against the server model.
            ExpressionNode node = DeserializeNode(request.SerializedQuery);
            Expression query = Rebind(node);

            // Execute against the server context.
            object? result = await ExecuteQueryAsync(query, request, cancellationToken).ConfigureAwait(false);

            // Map results to the wire format (entity rows vs columnar projections).
            return MapResults(result, request.ReturnsSingleResult);
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

        var results = new ArrayList();
        foreach (object? item in BuildQueryable(query))
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

    private QueryDataResult MapResults(object? result, bool singleResult)
    {
        List<object?> rows = ToRows(result, singleResult);

        // Entity-typed results → identity-keyed rows; projections → columnar data (E2 refines).
        Type? elementType = rows.FirstOrDefault(r => r is not null)?.GetType();
        bool isEntityResult = elementType is not null
            && _context.Model.FindEntityType(elementType) is not null;

        return new QueryDataResult
        {
            SerializedResults = SerializeResult(rows, elementType),
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
    private byte[] SerializeResult(List<object?> rows, Type? elementType)
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
        bool IsLoaded(object entity, INavigationBase navigation)
        {
            if (stateManager.TryGetEntry(entity) is { } entry)
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
        IEnumerable<object> ReadJoinEntities(object entity, ISkipNavigation skip)
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

                if (matches)
                {
                    yield return candidate.ToEntityEntry().Entity;
                }
            }
        }

        static bool HasKey(object entity, IEntityType entityType)
            => entityType.FindPrimaryKey() is not { } key
                || key.Properties.All(p => p.GetGetter().GetClrValue(entity) is not null);

        var nodes = new List<DynamicValueNode>();
        foreach (object? item in rows)
        {
            nodes.Add(item is null
                ? mapper.ToDynamicValue(null, elementType ?? typeof(object))
                : mapper.ToRowValue(item, item.GetType(), IsLoaded, IsTracked, ReadShadowValue, ReadJoinEntities, FindEntityType));
        }

        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            nodes, ExpressionJsonContext.Default.ListDynamicValueNode);
    }

    private static ExpressionNode DeserializeNode(byte[] payload)
        => System.Text.Json.JsonSerializer.Deserialize<ExpressionNode>(
            payload, ExpressionJsonContext.Default.ExpressionNode)!;
}
