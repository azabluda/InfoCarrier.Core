// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Linq.Expressions;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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

        // Deserialize + rebind the tree against the server model.
        ExpressionNode node = DeserializeNode(request.SerializedQuery);
        Expression query = Rebind(node);

        // Execute against the server context.
        object? result = await ExecuteQueryAsync(query, request, cancellationToken).ConfigureAwait(false);

        // Map results to the wire format (entity rows vs columnar projections).
        return MapResults(result, query);
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
            return GetQueryProvider(query).Execute(query);
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
        return (IQueryable)Activator.CreateInstance(queryableType, GetQueryProvider(query), query)!;
    }

    /// <summary>
    ///     Resolves the server query provider for a rebound tree, from the entity type of its
    ///     query <em>root</em>.
    /// </summary>
    /// <remarks>
    ///     Deriving the provider from the query's <em>result</em> type is wrong: a projection
    ///     (<c>Select(c =&gt; new { … })</c>, <c>Select(c =&gt; c.City)</c>) has a result type
    ///     the model knows nothing about, so the lookup threw
    ///     "Entity type '…' not found in the server model" before EF ever saw the query.
    ///     Every rebound tree is rooted in at least one real entity query root, and any of them
    ///     yields the same provider.
    /// </remarks>
    private IQueryProvider GetQueryProvider(Expression query)
    {
        IEntityType entityType = QueryRootFinder.Find(query)
            ?? throw new InvalidOperationException(
                "No entity query root found in the rebound query; cannot resolve a server query provider.");

        object set = _context
            .GetType()
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .First(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
            .MakeGenericMethod(entityType.ClrType)
            .Invoke(_context, null)!;
        return ((IQueryable)set).Provider;
    }

    /// <summary>
    ///     Finds the first entity query root in a rebound expression tree.
    /// </summary>
    private sealed class QueryRootFinder : ExpressionVisitor
    {
        private IEntityType? _entityType;

        public static IEntityType? Find(Expression query)
        {
            var finder = new QueryRootFinder();
            finder.Visit(query);
            return finder._entityType;
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (_entityType is null
                && node is Microsoft.EntityFrameworkCore.Query.EntityQueryRootExpression root)
            {
                _entityType = root.EntityType;
            }

            return _entityType is null ? base.VisitExtension(node) : node;
        }
    }

    private static Type GetElementType(Type queryType)
    {
        // Find the IQueryable<T> interface (handles IOrderedQueryable<T>, EntityQueryable<T>, …).
        Type? queryable = queryType.IsGenericType && queryType.GetGenericTypeDefinition() == typeof(IQueryable<>)
            ? queryType
            : queryType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));
        return queryable?.GetGenericArguments()[0] ?? queryType;
    }

    private QueryDataResult MapResults(object? result, Expression query)
    {
        // Entity-typed results → identity-keyed rows; projections → columnar data (E2 refines).
        object? first = result is IEnumerable enumerable
            ? enumerable.Cast<object?>().FirstOrDefault()
            : result;
        Type? elementType = first?.GetType();
        bool isEntityResult = elementType is not null
            && _context.Model.FindEntityType(elementType) is not null;

        return new QueryDataResult
        {
            SerializedResults = SerializeResult(result, elementType),
            IsEntityResult = isEntityResult,
            ElementTypeName = elementType?.FullName,
        };
    }

    private static byte[] SerializeResult(object? result, Type? elementType)
    {
        // Serialize as a typed List<elementType> so the client reconstructs typed rows.
        if (result is IEnumerable enumerable && elementType is not null)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            foreach (object? item in enumerable)
            {
                list.Add(item);
            }

            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(list, list.GetType());
        }

        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(result);
    }

    private static ExpressionNode DeserializeNode(byte[] payload)
        => System.Text.Json.JsonSerializer.Deserialize<ExpressionNode>(
            payload, ExpressionJsonContext.Default.ExpressionNode)!;
}
