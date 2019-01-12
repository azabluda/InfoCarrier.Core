// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Aqua.TypeSystem;
    using InfoCarrier.Core.Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Remote.Linq;
    using Remote.Linq.ExpressionVisitors;
    using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;
    using MethodInfo = System.Reflection.MethodInfo;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InfoCarrierQueryCompiler : IQueryCompiler
    {
        private static readonly MethodInfo CreateCompiledEnumerableQueryMethod
            = typeof(InfoCarrierQueryCompiler).GetTypeInfo().GetDeclaredMethod(nameof(CreateCompiledEnumerableQuery));

        private static readonly MethodInfo InterceptExceptionsMethod
            = new LinqOperatorProvider().InterceptExceptions;

        private static readonly MethodInfo AsyncInterceptExceptionsMethod
            = new AsyncLinqOperatorProvider().InterceptExceptions;

        private readonly IQueryContextFactory queryContextFactory;
        private readonly ICompiledQueryCache compiledQueryCache;
        private readonly ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator;
        private readonly IDiagnosticsLogger<DbLoggerCategory.Query> logger;
        private readonly ICurrentDbContext currentDbContext;
        private readonly IEvaluatableExpressionFilter evaluatableExpressionFilter;
        private readonly Lazy<IReadOnlyDictionary<string, IEntityType>> entityTypeMap;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1642:ConstructorSummaryDocumentationMustBeginWithStandardText", Justification = "Entity Framework Core internal.")]
        public InfoCarrierQueryCompiler(
            IQueryContextFactory queryContextFactory,
            ICompiledQueryCache compiledQueryCache,
            ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger,
            ICurrentDbContext currentDbContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter)
        {
            this.queryContextFactory = queryContextFactory;
            this.compiledQueryCache = compiledQueryCache;
            this.compiledQueryCacheKeyGenerator = compiledQueryCacheKeyGenerator;
            this.logger = logger;
            this.currentDbContext = currentDbContext;
            this.evaluatableExpressionFilter = evaluatableExpressionFilter;

            this.entityTypeMap = new Lazy<IReadOnlyDictionary<string, IEntityType>>(
                () => InfoCarrierQueryResultMapper.BuildEntityTypeMap(currentDbContext.Context));
        }

        private IReadOnlyDictionary<string, IEntityType> EntityTypeMap => this.entityTypeMap.Value;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1618:GenericTypeParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
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

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1618:GenericTypeParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public Func<QueryContext, Task<TResult>> CreateCompiledAsyncTaskQuery<TResult>(Expression query)
        {
            var compiledAsyncQuery = this.CreateCompiledAsyncEnumerableQuery<TResult>(query);
            return queryContext => AsyncEnumerableFirst(compiledAsyncQuery(queryContext), queryContext.CancellationToken);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1618:GenericTypeParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
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

            Type sequenceType = Utils.QueryReturnsSingleResult(query)
                ? null
                : typeof(TResult) == typeof(IEnumerable) ? typeof(object) : Utils.TryGetSequenceType(typeof(TResult));

            if (sequenceType == null)
            {
                return qc =>
                {
                    try
                    {
                        return this.CreateCompiledEnumerableQuery<TResult>(query)(qc).First();
                    }
                    catch (Exception exception)
                    {
                        this.logger.QueryIterationFailed(qc.Context.GetType(), exception);
                        throw;
                    }
                };
            }
            else
            {
                return (Func<QueryContext, TResult>)CreateCompiledEnumerableQueryMethod.MakeGenericMethod(sequenceType)
                    .Invoke(this, new object[] { query });
            }
        }

        private Func<QueryContext, IEnumerable<TItem>> CreateCompiledEnumerableQuery<TItem>(Expression query)
        {
            var preparedQuery = new PreparedQuery(query, this.EntityTypeMap);
            return queryContext =>
            {
                object items = preparedQuery.Execute<TItem>(queryContext);
                items = InterceptExceptionsMethod.MakeGenericMethod(typeof(TItem))
                    .Invoke(null, new[] { items, queryContext.Context.GetType(), this.logger, queryContext });
                return (IEnumerable<TItem>)items;
            };
        }

        private Expression ExtractParameters(
            Expression query,
            IParameterValues parameterValues,
            bool parameterize = true,
            bool generateContextAccessors = false)
        {
            var visitor
                = new ParameterExtractingExpressionVisitor(
                    this.evaluatableExpressionFilter,
                    parameterValues,
                    this.logger,
                    this.currentDbContext.Context,
                    parameterize,
                    generateContextAccessors);

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

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1618:GenericTypeParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public TResult Execute<TResult>(Expression query)
        {
            using (QueryContext queryContext = this.queryContextFactory.Create())
            {
                query = this.ExtractParameters(query, queryContext);
                return this.compiledQueryCache.GetOrAddQuery(
                    this.compiledQueryCacheKeyGenerator.GenerateCacheKey(query, false),
                    () => this.CreateCompiledQuery<TResult>(query, false)).Invoke(queryContext);
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1618:GenericTypeParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression query)
        {
            using (QueryContext queryContext = this.queryContextFactory.Create())
            {
                query = this.ExtractParameters(query, queryContext);
                return this.compiledQueryCache.GetOrAddAsyncQuery(
                    this.compiledQueryCacheKeyGenerator.GenerateCacheKey(query, true),
                    () => this.CreateCompiledAsyncEnumerableQuery<TResult>(query, false)).Invoke(queryContext);
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1618:GenericTypeParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public Task<TResult> ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken)
        {
            return AsyncEnumerableFirst(this.ExecuteAsync<TResult>(query), cancellationToken);
        }

        private sealed class PreparedQuery
        {
            private readonly Func<QueryContext, QueryExecutor> createQueryExecutor;

            public PreparedQuery(Expression expression, IReadOnlyDictionary<string, IEntityType> entityTypeMap)
            {
                this.createQueryExecutor = qc => new QueryExecutor(this, qc, entityTypeMap);

                // Replace NullConditionalExpression with NullConditionalExpressionStub MethodCallExpression
                expression = Utils.ReplaceNullConditional(expression, true);

                // Replace EntityQueryable with stub
                expression = EntityQueryableStubVisitor.Replace(expression);

                this.Expression = expression.SimplifyIncorporationOfRemoteQueryables();
            }

            private Expression Expression { get; }

            private ITypeResolver TypeResolver { get; } = new TypeResolver();

            private ITypeInfoProvider TypeInfoProvider { get; } = new TypeInfoProvider();

            public IEnumerable<TResult> Execute<TResult>(QueryContext queryContext)
                => this.createQueryExecutor(queryContext).ExecuteQuery<TResult>();

            public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryContext queryContext)
                => this.createQueryExecutor(queryContext).ExecuteAsyncQuery<TResult>();

            private sealed class QueryExecutor
            {
                private readonly QueryContext queryContext;
                private readonly Func<InfoCarrierQueryResultMapper> createResultMapper;
                private readonly IInfoCarrierClient infoCarrierClient;
                private readonly Remote.Linq.Expressions.Expression rlinq;

                public QueryExecutor(
                    PreparedQuery preparedQuery,
                    QueryContext queryContext,
                    IReadOnlyDictionary<string, IEntityType> entityTypeMap)
                {
                    this.queryContext = queryContext;
                    this.createResultMapper = () => new InfoCarrierQueryResultMapper(queryContext, preparedQuery.TypeResolver, preparedQuery.TypeInfoProvider, entityTypeMap);
                    this.infoCarrierClient = ((InfoCarrierQueryContext)queryContext).InfoCarrierClient;

                    // Substitute query parameters
                    Expression expression = new SubstituteParametersExpressionVisitor(queryContext).Visit(preparedQuery.Expression);

                    // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
                    this.rlinq = expression
                        .ToRemoteLinqExpression(preparedQuery.TypeInfoProvider, Remote.Linq.EntityFrameworkCore.ExpressionEvaluator.CanBeEvaluated)
                        .ReplaceQueryableByResourceDescriptors(preparedQuery.TypeInfoProvider)
                        .ReplaceGenericQueryArgumentsByNonGenericArguments();
                }

                public IEnumerable<TResult> ExecuteQuery<TResult>()
                {
                    QueryDataResult result = this.infoCarrierClient.QueryData(
                        new QueryDataRequest(
                            this.rlinq,
                            this.queryContext.Context.ChangeTracker.QueryTrackingBehavior),
                        this.queryContext.Context);
                    return this.createResultMapper().MapAndTrackResults<TResult>(result.MappedResults);
                }

                public IAsyncEnumerable<TResult> ExecuteAsyncQuery<TResult>()
                {
                    async Task<IEnumerable<TResult>> MapAndTrackResultsAsync()
                    {
                        QueryDataResult result = await this.infoCarrierClient.QueryDataAsync(
                            new QueryDataRequest(
                                this.rlinq,
                                this.queryContext.Context.ChangeTracker.QueryTrackingBehavior),
                            this.queryContext.Context,
                            this.queryContext.CancellationToken);
                        return this.createResultMapper().MapAndTrackResults<TResult>(result.MappedResults);
                    }

                    return new AsyncEnumerableAdapter<TResult>(MapAndTrackResultsAsync());
                }
            }

            private class SubstituteParametersExpressionVisitor : Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.ExpressionVisitorBase
            {
                private readonly QueryContext queryContext;

                public SubstituteParametersExpressionVisitor(QueryContext queryContext)
                {
                    this.queryContext = queryContext;
                }

                protected override Expression VisitParameter(ParameterExpression node)
                {
                    if (node.Name?.StartsWith(CompiledQueryCache.CompiledQueryParameterPrefix, StringComparison.Ordinal) == true)
                    {
                        object paramValue = Activator.CreateInstance(
                            typeof(ValueWrapper<>).MakeGenericType(node.Type),
                            this.queryContext.ParameterValues[node.Name]);

                        return Expression.Property(
                            Expression.Constant(paramValue),
                            nameof(ValueWrapper<object>.Value));
                    }

                    return base.VisitParameter(node);
                }

                private struct ValueWrapper<T>
                {
                    public ValueWrapper(T value) => this.Value = value;

                    public T Value { get; set; }
                }
            }

            private class EntityQueryableStubVisitor : ExpressionVisitorBase
            {
                internal static Expression Replace(Expression expression)
                    => new EntityQueryableStubVisitor().Visit(expression);

                protected override Expression VisitConstant(ConstantExpression constantExpression)
                    => constantExpression.IsEntityQueryable()
                        ? this.VisitEntityQueryable(((IQueryable)constantExpression.Value).ElementType)
                        : constantExpression;

                private Expression VisitEntityQueryable(Type elementType)
                {
                    object stub = Activator.CreateInstance(typeof(RemoteQueryableStub<>).MakeGenericType(elementType));
                    return Expression.Constant(stub);
                }

                [ExcludeFromCoverage]
                private class RemoteQueryableStub<T> : IRemoteQueryable, IQueryable<T>
                {
                    public Type ElementType => typeof(T);

                    public Expression Expression => throw new NotImplementedException();

                    public IQueryProvider Provider => throw new NotImplementedException();

                    public IEnumerator GetEnumerator() => throw new NotImplementedException();

                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotImplementedException();
                }
            }
        }
    }
}
