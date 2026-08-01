// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
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
    private readonly QueryTrackingBehavior _trackingBehavior;

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

        _trackingBehavior = TrackingBehaviorFinder.Find(
            query, queryContext.Context.ChangeTracker.QueryTrackingBehavior);
    }

    public object Execute(bool async, bool singleResult)
    {
        using var cs = _queryContext.ConcurrencyDetector.EnterCriticalSection();

        // Start of a message exchange: wire reference ids restart at 1 on both sides.
        ((Expressions.DynamicValueMapper)((ExpressionSerializer)_expressionSerializer).ValueMapper)
            .ResetReferenceScope();

        // Serialize the captured tree to the wire DTO.
        Expressions.ExpressionNode node = _expressionSerializer.ToNode(_query);
        var request = new QueryDataRequest
        {
            SerializedQuery = SerializeNode(node),
            TrackingBehavior = _trackingBehavior,
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
        // Client materialization + identity resolution (Phase E).
        => new ClientResultMaterializer(_queryContext.Context, _expressionSerializer, _trackingBehavior)
            .Materialize<TElement>(result);

    /// <summary>
    ///     Finds the tracking behavior a query asks for, falling back to the context default.
    /// </summary>
    /// <remarks>
    ///     <c>AsNoTracking()</c> and friends are ordinary method calls that EF strips in
    ///     <c>QueryableMethodNormalizingExpressionVisitor</c>, which runs *inside* query
    ///     compilation — so at the ADR-006 capture point they are still in the tree and this is
    ///     the only place the per-query behavior can be read. Reading only
    ///     <c>ChangeTracker.QueryTrackingBehavior</c> saw the context-wide default and silently
    ///     ignored every per-query <c>AsNoTracking()</c>.
    ///
    ///     Outermost wins, matching EF: it visits the inner query first and assigns afterwards,
    ///     so the last assignment — the outermost call — is the one that survives.
    /// </remarks>
    private sealed class TrackingBehaviorFinder : ExpressionVisitor
    {
        private QueryTrackingBehavior _behavior;

        public static QueryTrackingBehavior Find(Expression query, QueryTrackingBehavior fallback)
        {
            var finder = new TrackingBehaviorFinder { _behavior = fallback };
            finder.Visit(query);
            return finder._behavior;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Post-order: visit the inner query, then apply this call's marker.
            Expression visited = base.VisitMethodCall(node);

            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                && node.Arguments.Count == 1)
            {
                _behavior = node.Method.Name switch
                {
                    nameof(EntityFrameworkQueryableExtensions.AsTracking)
                        => QueryTrackingBehavior.TrackAll,
                    nameof(EntityFrameworkQueryableExtensions.AsNoTracking)
                        => QueryTrackingBehavior.NoTracking,
                    nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution)
                        => QueryTrackingBehavior.NoTrackingWithIdentityResolution,
                    _ => _behavior,
                };
            }

            return visited;
        }
    }

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
    ///     Substitutes EF query parameters as plain <see cref="ConstantExpression" />s of their
    ///     runtime values — never wrapped in custom generic structs (research-findings §6, the
    ///     v1 <c>ValueWrapper&lt;T&gt;</c> trap).
    /// </summary>
    /// <remarks>
    ///     Two node forms must be handled. EF Core 10's <c>ExpressionTreeFuncletizer</c> lifts
    ///     closure-captured values into <see cref="QueryParameterExpression" /> — an
    ///     <see cref="ExpressionType.Extension" /> node, so it arrives at
    ///     <see cref="VisitExtension" />, not <see cref="VisitParameter" />. Compiled queries
    ///     additionally produce ordinary <see cref="ParameterExpression" />s named with the
    ///     <c>__</c> prefix. The funcletizer keys
    ///     <see cref="QueryContext.Parameters" /> by the same name it gives the node, so a
    ///     single lookup serves both.
    /// </remarks>
    private sealed class SubstituteParametersExpressionVisitor : ExpressionVisitor
    {
        private readonly QueryContext _queryContext;

        public SubstituteParametersExpressionVisitor(QueryContext queryContext)
            => _queryContext = queryContext;

        protected override Expression VisitExtension(Expression node)
            => node is QueryParameterExpression queryParameter
                && _queryContext.Parameters.TryGetValue(queryParameter.Name, out object? value)
                    ? Expression.Constant(value, queryParameter.Type)
                    : base.VisitExtension(node);

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
