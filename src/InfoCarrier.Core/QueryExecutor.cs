// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core;

/// <summary>
///     Executes a captured query remotely: substitutes compiled-query parameters as plain
///     constants (research-findings §6), serializes the tree, ships it via
///     <see cref="IInfoCarrierClient" />, and materializes results (materialization lands in
///     Phase E). Nested in <see cref="InfoCarrierDatabase" />'s orbit (v1 QueryExecutor pattern).
/// </summary>
internal sealed class QueryExecutor<TElement>
{
    private readonly QueryContext _queryContext;
    private readonly IInfoCarrierClient _client;
    private readonly IExpressionSerializer _expressionSerializer;
    private readonly Expression _query;

    public QueryExecutor(
        QueryContext queryContext,
        Expression query,
        IInfoCarrierClient client,
        IExpressionSerializer expressionSerializer)
    {
        _queryContext = queryContext;
        _client = client;
        _expressionSerializer = expressionSerializer;

        // Substitute compiled-query parameters as plain constants (research-findings §6).
        _query = new SubstituteParametersExpressionVisitor(queryContext).Visit(query);
    }

    public object Execute(bool async, bool singleResult)
    {
        using var cs = _queryContext.ConcurrencyDetector.EnterCriticalSection();

        // Serialize the captured tree to the wire DTO.
        Expressions.ExpressionNode node = _expressionSerializer.ToNode(_query);
        var request = new QueryDataRequest
        {
            SerializedQuery = SerializeNode(node),
            TrackingBehavior = _queryContext.Context.ChangeTracker.QueryTrackingBehavior,
            IsAsync = async,
            ReturnsSingleResult = singleResult,
        };

        if (async)
        {
            var asyncEnum = ExecuteAsync(request);
            return singleResult ? (object)FirstOrDefaultAsync(asyncEnum) : asyncEnum;
        }

        QueryDataResult result = _client.QueryDataAsync(request).GetAwaiter().GetResult();
        var mapped = Materialize(result);
        return singleResult ? mapped.FirstOrDefault()! : mapped;
    }

    private async IAsyncEnumerable<TElement> ExecuteAsync(QueryDataRequest request)
    {
        QueryDataResult result = await _client.QueryDataAsync(request).ConfigureAwait(false);
        foreach (TElement element in Materialize(result))
        {
            yield return element;
        }
    }

    private IEnumerable<TElement> Materialize(QueryDataResult result)
        // Client materialization + identity resolution lands in Phase E (E1/E2).
        => throw new NotImplementedException("Client materialization lands in Phase E.");

    private static byte[] SerializeNode(Expressions.ExpressionNode node)
        => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            node, ExpressionJsonContext.Default.ExpressionNode);

    private static async Task<TElement> FirstOrDefaultAsync(IAsyncEnumerable<TElement> asyncEnum)
    {
        await foreach (TElement value in asyncEnum.ConfigureAwait(false))
        {
            return value;
        }

        return default!;
    }

    /// <summary>
    ///     Substitutes EF compiled-query parameters (named with the compiled-query prefix) as
    ///     plain <see cref="ConstantExpression" />s of their runtime values — never wrapped in
    ///     custom generic structs (research-findings §6, the v1 <c>ValueWrapper&lt;T&gt;</c> trap).
    /// </summary>
    private sealed class SubstituteParametersExpressionVisitor : ExpressionVisitor
    {
        private readonly QueryContext _queryContext;

        public SubstituteParametersExpressionVisitor(QueryContext queryContext)
            => _queryContext = queryContext;

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Name is not null
                && node.Name.StartsWith("__", StringComparison.Ordinal)
                && _queryContext.Parameters.TryGetValue(node.Name, out object? value))
            {
                return Expression.Constant(value, node.Type);
            }

            return base.VisitParameter(node);
        }
    }
}
