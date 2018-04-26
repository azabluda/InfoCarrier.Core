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
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public partial class InfoCarrierQueryCompiler : IQueryCompiler
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
        private readonly IEvaluatableExpressionFilter evaluatableExpressionFilter;
        private readonly Type contextType;
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
            ICurrentDbContext currentContext,
            IEvaluatableExpressionFilter evaluatableExpressionFilter)
        {
            this.queryContextFactory = queryContextFactory;
            this.compiledQueryCache = compiledQueryCache;
            this.compiledQueryCacheKeyGenerator = compiledQueryCacheKeyGenerator;
            this.logger = logger;
            this.evaluatableExpressionFilter = evaluatableExpressionFilter;
            this.contextType = currentContext.Context.GetType();

            this.entityTypeMap = new Lazy<IReadOnlyDictionary<string, IEntityType>>(() =>
            {
                using (QueryContext qc = this.queryContextFactory.Create())
                {
                     return qc.Context.Model.GetEntityTypes().ToDictionary(x => x.DisplayName());
                }
            });
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

            Type sequenceType =
                typeof(TResult) == typeof(IEnumerable)
                ? typeof(object)
                : Utils.TryGetQueryResultSequenceType(typeof(TResult));

            if (sequenceType == null)
            {
                return qc =>
                {
                    try
                    {
                        return this.CreateCompiledEnumerableQuery<TResult, IEnumerable<TResult>>(query)(qc).First();
                    }
                    catch (Exception exception)
                    {
                        this.logger.QueryIterationFailed(qc.Context.GetType(), exception);
                        throw;
                    }
                };
            }

            try
            {
                return (Func<QueryContext, TResult>)CreateCompiledEnumerableQueryMethod.MakeGenericMethod(sequenceType, typeof(TResult))
                    .Invoke(this, new object[] { query });
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private Func<QueryContext, TCollection> CreateCompiledEnumerableQuery<TItem, TCollection>(Expression query)
        {
            var preparedQuery = new PreparedQuery(query, this.EntityTypeMap);
            return queryContext =>
            {
                IEnumerable<TItem> result = preparedQuery.Execute<TItem>(queryContext);
                result = (IEnumerable<TItem>)InterceptExceptionsMethod.MakeGenericMethod(typeof(TItem))
                    .Invoke(null, new object[] { result, queryContext.Context.GetType(), this.logger, queryContext });

                if (result is TCollection collection)
                {
                    return collection;
                }

                // determine and materialize concrete collection type
                Type collType = new CollectionTypeFactory().TryFindTypeToInstantiate(typeof(TItem), typeof(TCollection)) ?? typeof(TCollection);
                return (TCollection)Activator.CreateInstance(collType, result);
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
                    this.contextType,
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
    }
}
