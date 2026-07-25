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
        // Build an IQueryable over the rebound tree and let EF compile/execute it.
        IQueryable queryable = BuildQueryable(query);

        if (request.ReturnsSingleResult)
        {
            object? single = null;
            foreach (object? item in queryable)
            {
                single = item;
                break;
            }

            return single;
        }

        var results = new ArrayList();
        foreach (object? item in queryable)
        {
            results.Add(item);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return results;
    }

    private IQueryable BuildQueryable(Expression query)
    {
        // Route the rebound tree through the server context's own query provider so EF
        // translates it against the real provider (SQL Server / InMemory / …). We obtain the
        // provider from a DbSet of the query's element entity type, then CreateQuery over the
        // rebound expression.
        Type elementType = GetElementType(query.Type);

        IQueryProvider provider = GetServerQueryProvider(elementType);
        Type queryableType = typeof(Microsoft.EntityFrameworkCore.Query.Internal.EntityQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, provider, query)!;
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

    private IQueryProvider GetServerQueryProvider(Type elementType)
    {
        // Resolve the entity type (shared-type by name fallback) and get its DbSet's provider.
        IEntityType? entityType = _context.Model.FindEntityType(elementType);
        if (entityType is null)
        {
            throw new InvalidOperationException($"Entity type '{elementType}' not found in the server model.");
        }

        object set = _context
            .GetType()
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .First(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
            .MakeGenericMethod(entityType.ClrType)
            .Invoke(_context, null)!;
        return ((IQueryable)set).Provider;
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
