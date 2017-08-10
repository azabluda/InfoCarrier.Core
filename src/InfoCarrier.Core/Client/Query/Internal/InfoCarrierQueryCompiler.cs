namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;

    public partial class InfoCarrierQueryCompiler : IQueryCompiler
    {
        private static readonly MethodInfo CreateCompiledEnumerableQueryMethod
            = typeof(InfoCarrierQueryCompiler).GetTypeInfo().GetDeclaredMethod(nameof(CreateCompiledEnumerableQuery));

        private static readonly MethodInfo InterceptExceptionsMethod
            = new LinqOperatorProvider().InterceptExceptions;

        private static readonly MethodInfo AsyncInterceptExceptionsMethod
            = new AsyncLinqOperatorProvider().InterceptExceptions;

        private static readonly IEvaluatableExpressionFilter EvaluatableExpressionFilter
            = new EvaluatableExpressionFilter();

        private readonly IQueryContextFactory queryContextFactory;
        private readonly ICompiledQueryCache compiledQueryCache;
        private readonly ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator;
        private readonly IInterceptingLogger<LoggerCategory.Query> logger;
        private readonly Lazy<IReadOnlyDictionary<string, IEntityType>> entityTypeMap;

        public InfoCarrierQueryCompiler(
            IQueryContextFactory queryContextFactory,
            ICompiledQueryCache compiledQueryCache,
            ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
            IInterceptingLogger<LoggerCategory.Query> logger)
        {
            this.queryContextFactory = queryContextFactory;
            this.compiledQueryCache = compiledQueryCache;
            this.compiledQueryCacheKeyGenerator = compiledQueryCacheKeyGenerator;
            this.logger = logger;

            this.entityTypeMap = new Lazy<IReadOnlyDictionary<string, IEntityType>>(() =>
            {
                using (QueryContext qc = this.queryContextFactory.Create())
                {
                     return qc.Context.Model.GetEntityTypes().ToDictionary(x => x.DisplayName());
                }
            });
        }

        private IReadOnlyDictionary<string, IEntityType> EntityTypeMap => this.entityTypeMap.Value;

        public Func<QueryContext, IAsyncEnumerable<TResult>> CreateCompiledAsyncEnumerableQuery<TResult>(Expression query)
            => this.CreateCompiledAsyncEnumerableQuery<TResult>(query, true);

        private Func<QueryContext, IAsyncEnumerable<TResult>> CreateCompiledAsyncEnumerableQuery<TResult>(Expression query, bool extractParams)
        {
            if (extractParams)
            {
                using (QueryContext qc = this.queryContextFactory.Create())
                {
                    query = this.ExtractParameters(query, qc, false);
                }
            }

            var preparedQuery = new PreparedQuery(query, this.EntityTypeMap);
            return queryContext =>
            {
                IAsyncEnumerable<TResult> result = preparedQuery.ExecuteAsync<TResult>(queryContext);
                return (IAsyncEnumerable<TResult>)AsyncInterceptExceptionsMethod.MakeGenericMethod(typeof(TResult))
                    .Invoke(null, new object[] { result, queryContext.Context.GetType(), this.logger, queryContext });
            };
        }

        public Func<QueryContext, Task<TResult>> CreateCompiledAsyncTaskQuery<TResult>(Expression query)
        {
            var compiledAsyncQuery = this.CreateCompiledAsyncEnumerableQuery<TResult>(query);
            return queryContext => AsyncEnumerableFirst(compiledAsyncQuery(queryContext), queryContext.CancellationToken);
        }

        public Func<QueryContext, TResult> CreateCompiledQuery<TResult>(Expression query)
            => this.CreateCompiledQuery<TResult>(query, true);

        private Func<QueryContext, TResult> CreateCompiledQuery<TResult>(Expression query, bool extractParams)
        {
            if (extractParams)
            {
                using (QueryContext qc = this.queryContextFactory.Create())
                {
                    query = this.ExtractParameters(query, qc, false);
                }
            }

            Type sequenceType =
                typeof(TResult) == typeof(IEnumerable)
                ? typeof(object)
                : Utils.TryGetQueryResultSequenceType(typeof(TResult));

            if (sequenceType == null)
            {
                return queryContext => this.CreateCompiledEnumerableQuery<TResult>(query)(queryContext).First();
            }

            try
            {
                return (Func<QueryContext, TResult>)CreateCompiledEnumerableQueryMethod.MakeGenericMethod(sequenceType)
                    .Invoke(this, new object[] { query });
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private Func<QueryContext, IEnumerable<TResult>> CreateCompiledEnumerableQuery<TResult>(Expression query)
        {
            var preparedQuery = new PreparedQuery(query, this.EntityTypeMap);
            return queryContext =>
            {
                IEnumerable<TResult> result = preparedQuery.Execute<TResult>(queryContext);
                return (IEnumerable<TResult>)InterceptExceptionsMethod.MakeGenericMethod(typeof(TResult))
                    .Invoke(null, new object[] { result, queryContext.Context.GetType(), this.logger, queryContext });
            };
        }

        private Expression ExtractParameters(
            Expression query,
            QueryContext queryContext,
            bool parameterize)
        {
            var visitor
                = new ParameterExtractingExpressionVisitor(
                    EvaluatableExpressionFilter,
                    queryContext,
                    this.logger,
                    parameterize);

            return visitor.ExtractParameters(query);
        }

        private static async Task<TResult> AsyncEnumerableFirst<TResult>(
            IAsyncEnumerable<TResult> asyncEnumerable,
            CancellationToken cancellationToken)
        {
            using (var asyncEnumerator = asyncEnumerable.GetEnumerator())
            {
                await asyncEnumerator.MoveNext(cancellationToken);
                return asyncEnumerator.Current;
            }
        }

        public TResult Execute<TResult>(Expression query)
        {
            using (QueryContext queryContext = this.queryContextFactory.Create())
            {
                query = this.ExtractParameters(query, queryContext, true);
                return this.compiledQueryCache.GetOrAddQuery(
                    this.compiledQueryCacheKeyGenerator.GenerateCacheKey(query, false),
                    () => this.CreateCompiledQuery<TResult>(query, false)).Invoke(queryContext);
            }
        }

        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression query)
        {
            using (QueryContext queryContext = this.queryContextFactory.Create())
            {
                query = this.ExtractParameters(query, queryContext, true);
                return this.compiledQueryCache.GetOrAddAsyncQuery(
                    this.compiledQueryCacheKeyGenerator.GenerateCacheKey(query, true),
                    () => this.CreateCompiledAsyncEnumerableQuery<TResult>(query, false)).Invoke(queryContext);
            }
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken)
        {
            return AsyncEnumerableFirst(this.ExecuteAsync<TResult>(query), cancellationToken);
        }
    }
}
