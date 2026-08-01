// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    private static readonly System.Reflection.MethodInfo MaterializeMethod
        = typeof(ClientResultMaterializer).GetMethod(nameof(ClientResultMaterializer.Materialize))!;

    private static readonly System.Reflection.MethodInfo ToListMethod
        = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!;

    private static readonly System.Reflection.MethodInfo AsQueryableMethod
        = typeof(Queryable).GetMethods()
            .Single(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethodDefinition);

    private readonly QueryContext _queryContext;
    private readonly IInfoCarrierClient _client;
    private readonly IExpressionSerializer _expressionSerializer;
    private readonly SplitQuery _split;
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
        Expression substituted = new SubstituteParametersExpressionVisitor(queryContext).Visit(query);

        // Then, and only then, decide what the server can execute: a surviving closure field
        // access names a compiler-generated display class and would push the boundary in for
        // no reason (ADR-010, docs/projection-split.md).
        _split = new QuerySplitter(queryContext.Context.Model).Split(substituted);

        _trackingBehavior = TrackingBehaviorFinder.Find(
            query, queryContext.Context.ChangeTracker.QueryTrackingBehavior);
    }

    public object Execute(bool async, bool singleResult)
    {
        if (async)
        {
            // The critical section belongs *inside*: this method only builds the enumerable,
            // and a `using` here would release before a single row had been fetched. EF holds
            // it across each MoveNext for the same reason.
            IAsyncEnumerable<TElement> asyncEnum = ExecuteAsync(singleResult);
            return singleResult ? (object)FirstOrDefaultAsync(asyncEnum) : asyncEnum;
        }

        using var criticalSection = _queryContext.ConcurrencyDetector.EnterCriticalSection();

        var results = new List<object?>(_split.ServerQueries.Count);
        foreach (ServerQuery serverQuery in _split.ServerQueries)
        {
            QueryDataResult result = _client
                .QueryDataAsync(BuildRequest(serverQuery, async: false), _queryContext.CancellationToken)
                .GetAwaiter()
                .GetResult();
            results.Add(Materialize(serverQuery, result));
        }

        IEnumerable<TElement> mapped = ApplyResidual(results, singleResult);
        return singleResult ? mapped.FirstOrDefault()! : mapped;
    }

    private async IAsyncEnumerable<TElement> ExecuteAsync(bool singleResult)
    {
        CancellationToken cancellationToken = _queryContext.CancellationToken;

        var results = new List<object?>(_split.ServerQueries.Count);
        foreach (ServerQuery serverQuery in _split.ServerQueries)
        {
            QueryDataResult result;
            try
            {
                using (_queryContext.ConcurrencyDetector.EnterCriticalSection())
                {
                    // Checked before the round-trip, as EF does in MoveNextAsync: an
                    // already-cancelled token must surface as OperationCanceledException rather
                    // than a completed query.
                    cancellationToken.ThrowIfCancellationRequested();
                    result = await _client
                        .QueryDataAsync(BuildRequest(serverQuery, async: true), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                // Same reporting EF's own query enumerators do — callers (and the spec tests)
                // observe cancellation through the CoreEventId.QueryCanceled log event, not only
                // through the thrown exception.
                Type contextType = _queryContext.Context.GetType();
                if (_queryContext.ExceptionDetector.IsCancellation(exception, cancellationToken))
                {
                    _queryContext.QueryLogger.QueryCanceled(contextType);
                }
                else
                {
                    _queryContext.QueryLogger.QueryIterationFailed(contextType, exception);
                }

                throw;
            }

            results.Add(Materialize(serverQuery, result));
        }

        foreach (TElement element in ApplyResidual(results, singleResult))
        {
            yield return element;
        }
    }

    private QueryDataRequest BuildRequest(ServerQuery serverQuery, bool async)
    {
        // Start of a message exchange: wire reference ids restart at 1 on both sides. One scope
        // per round trip, so a split query's second request does not decode against the first's
        // reference table.
        ((Expressions.DynamicValueMapper)((ExpressionSerializer)_expressionSerializer).ValueMapper)
            .ResetReferenceScope();

        return new QueryDataRequest
        {
            SerializedQuery = SerializeNode(_expressionSerializer.ToNode(serverQuery.Query)),
            TrackingBehavior = _trackingBehavior,
            IsAsync = async,
            ReturnsSingleResult = serverQuery.ReturnsSingleResult,
        };
    }

    /// <summary>
    ///     Turns one server result into what the residual expects for that slot.
    /// </summary>
    /// <remarks>
    ///     The boundary element type is not <typeparamref name="TElement" /> whenever a residual
    ///     exists, so materialization goes through the runtime type. Rows are drained eagerly:
    ///     the wire reference scope belongs to one round trip, and a lazily materialized sequence
    ///     would decode against whichever scope the *next* request had reset.
    /// </remarks>
    private object? Materialize(ServerQuery serverQuery, QueryDataResult result)
    {
        var materializer = new ClientResultMaterializer(
            _queryContext.Context, _expressionSerializer, _trackingBehavior);

        object rows = MaterializeMethod
            .MakeGenericMethod(serverQuery.ElementType)
            .Invoke(materializer, [result])!;
        object list = ToListMethod.MakeGenericMethod(serverQuery.ElementType).Invoke(null, [rows])!;

        if (serverQuery.ReturnsSingleResult)
        {
            return ((System.Collections.IEnumerable)list).Cast<object?>().FirstOrDefault();
        }

        return AsQueryableMethod.MakeGenericMethod(serverQuery.ElementType).Invoke(null, [list])!;
    }

    private IEnumerable<TElement> ApplyResidual(IReadOnlyList<object?> results, bool singleResult)
    {
        if (_split.IsPassThrough)
        {
            // Nothing to apply. Keep the untouched path allocation-for-allocation what it was
            // before the split existed.
            return results[0] is IEnumerable<TElement> sequence ? sequence : [(TElement)results[0]!];
        }

        object? applied = _split.Apply(results);

        return singleResult
            ? [(TElement)applied!]
            : ((System.Collections.IEnumerable)applied!).Cast<TElement>();
    }

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
