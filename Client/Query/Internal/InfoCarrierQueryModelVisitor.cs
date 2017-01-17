namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Query.ResultOperators.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Remote.Linq;
    using Remote.Linq.ExpressionVisitors;
    using Remotion.Linq;
    using Remotion.Linq.Clauses;

    public partial class InfoCarrierQueryModelVisitor : EntityQueryModelVisitor
    {
        public InfoCarrierQueryModelVisitor(
            IQueryOptimizer queryOptimizer,
            INavigationRewritingExpressionVisitorFactory navigationRewritingExpressionVisitorFactory,
            ISubQueryMemberPushDownExpressionVisitor subQueryMemberPushDownExpressionVisitor,
            IQuerySourceTracingExpressionVisitorFactory querySourceTracingExpressionVisitorFactory,
            IEntityResultFindingExpressionVisitorFactory entityResultFindingExpressionVisitorFactory,
            ITaskBlockingExpressionVisitor taskBlockingExpressionVisitor,
            IMemberAccessBindingExpressionVisitorFactory memberAccessBindingExpressionVisitorFactory,
            IOrderingExpressionVisitorFactory orderingExpressionVisitorFactory,
            IProjectionExpressionVisitorFactory projectionExpressionVisitorFactory,
            IEntityQueryableExpressionVisitorFactory entityQueryableExpressionVisitorFactory,
            IQueryAnnotationExtractor queryAnnotationExtractor,
            IResultOperatorHandler resultOperatorHandler,
            IEntityMaterializerSource entityMaterializerSource,
            IExpressionPrinter expressionPrinter,
            QueryCompilationContext queryCompilationContext)
            : base(
                queryOptimizer, // not used, see OptimizeQueryModel
                navigationRewritingExpressionVisitorFactory, // not used, see OptimizeQueryModel
                subQueryMemberPushDownExpressionVisitor, // not used, see OptimizeQueryModel
                querySourceTracingExpressionVisitorFactory,
                entityResultFindingExpressionVisitorFactory,
                taskBlockingExpressionVisitor,
                memberAccessBindingExpressionVisitorFactory,
                orderingExpressionVisitorFactory,
                projectionExpressionVisitorFactory,
                entityQueryableExpressionVisitorFactory,
                queryAnnotationExtractor,
                resultOperatorHandler,
                entityMaterializerSource,
                expressionPrinter,
                queryCompilationContext)
        {
        }

        private bool ExpressionIsQueryable =>
            this.Expression != null
            && ImplementsGenericInterface(this.Expression.Type, typeof(IQueryable<>));

        private bool ExpressionIsAsyncEnumerable =>
            this.Expression != null
            && ImplementsGenericInterface(this.Expression.Type, typeof(IAsyncEnumerable<>));

        internal virtual InfoCarrierLinqOperatorProvider InfoCarrierLinqOperatorProvider =>
            this.ExpressionIsQueryable
                ? InfoCarrierQueryableLinqOperatorProvider.Instance
                : InfoCarrierEnumerableLinqOperatorProvider.Instance;

        public override ILinqOperatorProvider LinqOperatorProvider =>
            this.ExpressionIsAsyncEnumerable
                ? (ILinqOperatorProvider)new AsyncLinqOperatorProvider()
                : this.InfoCarrierLinqOperatorProvider;

        protected override void OptimizeQueryModel(QueryModel queryModel)
        {
            // Suppress any optimization. We want to transparently transfer the query
            // to the application server as close to the original as it is possible.
        }

        public override Func<QueryContext, IAsyncEnumerable<TResult>> CreateAsyncQueryExecutor<TResult>(QueryModel queryModel)
        {
            // UGLY: pretty much copy-and-paste of the base implementation except for:
            // + Call SingleResultToSequence without 2nd argument
            // - Unable to "copy-and-paste" original logging
            using (this.QueryCompilationContext.Logger.BeginScope(this))
            {
                this.ExtractQueryAnnotations(queryModel);

                this.OptimizeQueryModel(queryModel);

                this.QueryCompilationContext.FindQuerySourcesRequiringMaterialization(this, queryModel);
                this.QueryCompilationContext.DetermineQueryBufferRequirement(queryModel);

                this.VisitQueryModel(queryModel);

                this.SingleResultToSequence(queryModel);

                this.IncludeNavigations(queryModel);

                this.TrackEntitiesInResults<TResult>(queryModel);

                this.InterceptExceptions();

                return this.CreateExecutorLambda<IAsyncEnumerable<TResult>>();
            }
        }

        private static IAsyncEnumerable<TResult> ExecuteAsyncQuery<TResult>(
            QueryContext queryContext,
            Expression expression,
            bool queryStateManager)
        {
            return new QueryExecutor<TResult>(queryContext, expression, queryStateManager)
                .ExecuteAsyncQuery();
        }

        private static IEnumerable<TResult> ExecuteQuery<TResult>(
            QueryContext queryContext,
            Expression expression,
            bool queryStateManager)
        {
            return new QueryExecutor<TResult>(queryContext, expression, queryStateManager)
                .ExecuteQuery();
        }

        protected override void SingleResultToSequence(QueryModel queryModel, Type type = null)
        {
            // Add .Include to the expression before its execution
            foreach (var incl in this.QueryCompilationContext.QueryAnnotations.OfType<IncludeResultOperator>())
            {
                this.Expression = new InfoCarrierIncludeExpressionVisitor(incl).Visit(this.Expression);
            }

            MethodInfo execQueryMethod =
                ((InfoCarrierQueryCompilationContext)this.QueryCompilationContext).Async
                    ? MethodInfoExtensions.GetMethodInfo(() => ExecuteAsyncQuery<object>(null, null, false))
                    : MethodInfoExtensions.GetMethodInfo(() => ExecuteQuery<object>(null, null, false));

            // Prevent misinterpretation of single element T[] as collection of T
            Type resultType = this.Expression.Type.IsArray
                ? this.Expression.Type
                : Server.QueryDataHelper.GetSequenceType(this.Expression.Type, this.Expression.Type);

            this.Expression
                = Expression.Call(
                    execQueryMethod
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(resultType),
                    QueryContextParameter,
                    Expression.Constant(this.Expression),
                    Expression.Constant(this.QueryCompilationContext.IsTrackingQuery));
        }

        public override void VisitAdditionalFromClause(
            AdditionalFromClause fromClause,
            QueryModel queryModel,
            int index)
        {
            // UGLY: this method is a shameless copy-and-paste of
            // https://github.com/aspnet/EntityFramework/blob/1.0.0/src/Microsoft.EntityFrameworkCore/Query/EntityQueryModelVisitor.cs#L709
            // We need to replace EFC's TransparentIdentifiers with regular anonymous types,
            // otherwise Remote.Linq's serializaion would fail.
            // Additionally we determine and use 'firstParamDelegateType' in ExpressionIsQueryable case.
            var fromExpression
                = this.CompileAdditionalFromClauseExpression(fromClause, queryModel);

            var innerItemParameter
                = Expression.Parameter(
                    fromExpression.Type.GetSequenceType(), fromClause.ItemName);

            var transparentIdentifierType = GetTransparentIdentifierType(
                this.CurrentParameter.Type,
                innerItemParameter.Type);

            MethodInfo miSelectMany
                = this.LinqOperatorProvider.SelectMany
                    .MakeGenericMethod(
                        this.CurrentParameter.Type,
                        innerItemParameter.Type,
                        transparentIdentifierType);

            Type firstParamDelegateType = miSelectMany.GetParameters()[1].ParameterType;
            if (this.ExpressionIsQueryable)
            {
                firstParamDelegateType = firstParamDelegateType.GenericTypeArguments[0];
            }

            this.Expression
                = Expression.Call(
                    miSelectMany,
                    this.Expression,
                    Expression.Lambda(firstParamDelegateType, fromExpression, this.CurrentParameter),
                    Expression.Lambda(
                        CallCreateTransparentIdentifier(
                            transparentIdentifierType, this.CurrentParameter, innerItemParameter),
                        this.CurrentParameter,
                        innerItemParameter));

            this.IntroduceTransparentScope(fromClause, queryModel, index, transparentIdentifierType);
        }

        public override void VisitOrdering(Ordering ordering, QueryModel queryModel, OrderByClause orderByClause, int index)
        {
            Expression expression = this.ReplaceClauseReferences(ordering.Expression);

            MethodInfo miOrdering = index == 0
                ? (ordering.OrderingDirection == OrderingDirection.Asc
                    ? this.InfoCarrierLinqOperatorProvider.OrderBy
                    : this.InfoCarrierLinqOperatorProvider.OrderByDescending)
                : (ordering.OrderingDirection == OrderingDirection.Asc
                    ? this.InfoCarrierLinqOperatorProvider.ThenBy
                    : this.InfoCarrierLinqOperatorProvider.ThenByDescending);

            this.Expression
                = Expression.Call(
                    miOrdering.MakeGenericMethod(this.CurrentParameter.Type, expression.Type),
                    this.Expression,
                    Expression.Lambda(expression, this.CurrentParameter));
        }

        public override void VisitJoinClause(JoinClause joinClause, QueryModel queryModel, int index)
        {
            // UGLY: this method is a shameless copy-and-paste of
            // https://github.com/aspnet/EntityFramework/blob/1.0.0/src/Microsoft.EntityFrameworkCore/Query/EntityQueryModelVisitor.cs#L765
            // We need to replace EFC's TransparentIdentifiers with regular anonymous types,
            // otherwise Remote.Linq's serializaion would fail.
            var outerKeySelectorExpression
                = this.ReplaceClauseReferences(joinClause.OuterKeySelector, joinClause);

            var innerSequenceExpression
                = this.CompileJoinClauseInnerSequenceExpression(joinClause, queryModel);

            var innerItemParameter
                = Expression.Parameter(
                    innerSequenceExpression.Type.GetSequenceType(), joinClause.ItemName);

            if (!this.QueryCompilationContext.QuerySourceMapping.ContainsMapping(joinClause))
            {
                this.QueryCompilationContext.QuerySourceMapping
                    .AddMapping(joinClause, innerItemParameter);
            }

            var innerKeySelectorExpression
                = this.ReplaceClauseReferences(joinClause.InnerKeySelector, joinClause);

            var transparentIdentifierType = GetTransparentIdentifierType(
                this.CurrentParameter.Type,
                innerItemParameter.Type);

            this.Expression
                = Expression.Call(
                    this.LinqOperatorProvider.Join
                        .MakeGenericMethod(
                            this.CurrentParameter.Type,
                            innerItemParameter.Type,
                            outerKeySelectorExpression.Type,
                            transparentIdentifierType),
                    this.Expression,
                    innerSequenceExpression,
                    Expression.Lambda(outerKeySelectorExpression, this.CurrentParameter),
                    Expression.Lambda(innerKeySelectorExpression, innerItemParameter),
                    Expression.Lambda(
                        CallCreateTransparentIdentifier(
                            transparentIdentifierType,
                            this.CurrentParameter,
                            innerItemParameter),
                        this.CurrentParameter,
                        innerItemParameter));

            this.IntroduceTransparentScope(joinClause, queryModel, index, transparentIdentifierType);
        }

        public override void VisitGroupJoinClause(GroupJoinClause groupJoinClause, QueryModel queryModel, int index)
        {
            // UGLY: this method is a shameless copy-and-paste of
            // https://github.com/aspnet/EntityFramework/blob/1.0.0/src/Microsoft.EntityFrameworkCore/Query/EntityQueryModelVisitor.cs#L838
            // We need to replace EFC's TransparentIdentifiers with regular anonymous types,
            // otherwise Remote.Linq's serializaion would fail.
            var outerKeySelectorExpression
                = this.ReplaceClauseReferences(groupJoinClause.JoinClause.OuterKeySelector, groupJoinClause);

            var innerSequenceExpression
                = this.CompileGroupJoinInnerSequenceExpression(groupJoinClause, queryModel);

            var innerItemParameter
                = Expression.Parameter(
                    innerSequenceExpression.Type.GetSequenceType(),
                    groupJoinClause.JoinClause.ItemName);

            if (!this.QueryCompilationContext.QuerySourceMapping.ContainsMapping(groupJoinClause.JoinClause))
            {
                this.QueryCompilationContext.QuerySourceMapping
                    .AddMapping(groupJoinClause.JoinClause, innerItemParameter);
            }
            else
            {
                this.QueryCompilationContext.QuerySourceMapping
                    .ReplaceMapping(groupJoinClause.JoinClause, innerItemParameter);
            }

            var innerKeySelectorExpression
                = this.ReplaceClauseReferences(groupJoinClause.JoinClause.InnerKeySelector, groupJoinClause);

            var innerItemsParameter
                = Expression.Parameter(
                    this.LinqOperatorProvider.MakeSequenceType(innerItemParameter.Type),
                    groupJoinClause.ItemName);

            var transparentIdentifierType
                = GetTransparentIdentifierType(this.CurrentParameter.Type, innerItemsParameter.Type);

            this.Expression
                = Expression.Call(
                    this.LinqOperatorProvider.GroupJoin
                        .MakeGenericMethod(
                            this.CurrentParameter.Type,
                            innerItemParameter.Type,
                            outerKeySelectorExpression.Type,
                            transparentIdentifierType),
                    this.Expression,
                    innerSequenceExpression,
                    Expression.Lambda(outerKeySelectorExpression, this.CurrentParameter),
                    Expression.Lambda(innerKeySelectorExpression, innerItemParameter),
                    Expression.Lambda(
                        CallCreateTransparentIdentifier(
                            transparentIdentifierType,
                            this.CurrentParameter,
                            innerItemsParameter),
                        this.CurrentParameter,
                        innerItemsParameter));

            this.IntroduceTransparentScope(groupJoinClause, queryModel, index, transparentIdentifierType);
        }

        protected override void IncludeNavigations(
            IncludeSpecification includeSpecification,
            Type resultType,
            Expression accessorExpression,
            bool querySourceRequiresTracking)
        {
            // Everything is already in-place so we just stub this method with empty body
        }

        private static bool ImplementsGenericInterface(Type type, Type interfaceType)
        {
            if (type.IsInterface
                && type.IsGenericType
                && type.GetGenericTypeDefinition() == interfaceType)
            {
                return true;
            }

            return type.GetInterfaces().Any(i => ImplementsGenericInterface(i, interfaceType));
        }

        private sealed class QueryExecutor<TResult> : DynamicObjectMapper
        {
            private readonly QueryContext queryContext;
            private readonly bool queryStateManager;
            private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();
            private readonly IInfoCarrierBackend infoCarrierBackend;
            private readonly Remote.Linq.Expressions.Expression rlinq;

            public QueryExecutor(
                QueryContext queryContext,
                Expression expression,
                bool queryStateManager)
                : base(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true })
            {
                this.queryContext = queryContext;
                this.queryStateManager = queryStateManager;
                this.infoCarrierBackend = ((InfoCarrierQueryContext)queryContext).InfoCarrierBackend;

                // Substitute query parameters
                expression = new SubstituteParametersExpressionVisitor(queryContext).Visit(expression);

                // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
                this.rlinq = expression
                    .ToRemoteLinqExpression()
                    .ReplaceQueryableByResourceDescriptors()
                    .ReplaceGenericQueryArgumentsByNonGenericArguments();
            }

            public IEnumerable<TResult> ExecuteQuery()
            {
                IEnumerable<DynamicObject> dataRecords = this.infoCarrierBackend.QueryData(this.rlinq);
                if (dataRecords == null)
                {
                    return Enumerable.Empty<TResult>();
                }

                return this.Map<TResult>(dataRecords);
            }

            public IAsyncEnumerable<TResult> ExecuteAsyncQuery()
            {
                return new AsyncEnumerableAdapter<TResult>(this.infoCarrierBackend.QueryDataAsync(this.rlinq), this);
            }

            protected override object MapFromDynamicObjectGraph(object obj, Type targetType)
            {
                object entity;
                if (this.TryMapEntity(obj, out entity))
                {
                    return entity;
                }

                if (obj != null &&
                    targetType.IsGenericType &&
                    targetType.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    object list = base.MapFromDynamicObjectGraph(
                        obj,
                        typeof(List<>).MakeGenericType(targetType.GenericTypeArguments));

                    return Activator.CreateInstance(
                        new CollectionTypeFactory().TryFindTypeToInstantiate(targetType.GenericTypeArguments.Single(), targetType),
                        list);
                }

                return base.MapFromDynamicObjectGraph(obj, targetType);
            }

            private bool TryMapEntity(object obj, out object entity)
            {
                var dobj = obj as DynamicObject;
                if (dobj == null)
                {
                    entity = null;
                    return false;
                }

                object entityTypeName;
                if (!dobj.TryGet(Server.QueryDataHelper.EntityTypeNameTag, out entityTypeName))
                {
                    entity = null;
                    return false;
                }

                if (!(entityTypeName is string))
                {
                    entity = null;
                    return false;
                }

                IEntityType entityType = this.queryContext.StateManager.Value.Context.Model.FindEntityType(entityTypeName.ToString());
                if (entityType == null)
                {
                    entity = null;
                    return false;
                }

                if (this.map.TryGetValue(dobj, out entity))
                {
                    return true;
                }

                IKey key = entityType.FindPrimaryKey();

                // TRICKY: We need ValueBuffer containing only key values (for entity identity lookup)
                // and shadow property values for InternalMixedEntityEntry.
                // We will set other properties with our own algorithm.
                var keyAndShadowProps = entityType.GetProperties().Where(p => p.IsKey() || p.IsShadowProperty).ToList();
                var nulls = Enumerable.Repeat<object>(null, 1 + keyAndShadowProps.Select(p => p.GetIndex()).DefaultIfEmpty(-1).Max());
                var valueBuffer = new ValueBuffer(nulls.ToList());
                foreach (IProperty p in keyAndShadowProps)
                {
                    valueBuffer[p.GetIndex()] = this.MapFromDynamicObjectGraph(dobj.Get(p.Name), p.ClrType);
                }

                // Get entity instance from EFC's identity map, or create a new one
                entity = this.queryContext
                    .QueryBuffer
                    .GetEntity(
                        key,
                        new EntityLoadInfo(
                            valueBuffer,
                            vr =>
                            {
                                object newEntity = Activator.CreateInstance(entityType.ClrType);
                                this.map.Add(dobj, newEntity);

                                // Set regular (non-shadow) properties
                                var targetProperties = newEntity.GetType().GetProperties().Where(p => p.CanWrite);
                                foreach (PropertyInfo property in targetProperties)
                                {
                                    object value;
                                    if (dobj.TryGet(property.Name, out value))
                                    {
                                        value = this.MapFromDynamicObjectGraph(value, property.PropertyType);
                                        property.SetValue(newEntity, value);
                                    }
                                }

                                return newEntity;
                            }),
                        this.queryStateManager,
                        throwOnNullKey: false);
                return true;
            }
        }

        private class InfoCarrierIncludeExpressionVisitor : Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.ExpressionVisitorBase
        {
            private readonly IncludeResultOperator includeResultOperator;

            public InfoCarrierIncludeExpressionVisitor(IncludeResultOperator includeResultOperator)
            {
                this.includeResultOperator = includeResultOperator;
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                var stub = node.Value as InfoCarrierEntityQueryableExpressionVisitor.RemoteQueryableStub;
                if (stub?.QuerySource == this.includeResultOperator.QuerySource)
                {
                    return this.ApplyTopLevelInclude(node);
                }

                return base.VisitConstant(node);
            }

            private Expression ApplyTopLevelInclude(ConstantExpression constantExpression)
            {
                Type entityType = constantExpression.Type.GetGenericArguments().Single();
                Type toType = this.includeResultOperator.NavigationPropertyPath.Type;

                var arg = Expression.Parameter(entityType, this.includeResultOperator.QuerySource.ItemName);

                var methodCallExpression = Expression.Call(
                    MethodInfoExtensions.GetMethodInfo(() => EntityFrameworkQueryableExtensions.Include<object, object>(null, null))
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(entityType, toType),
                    constantExpression,
                    Expression.Lambda(
                        Expression.MakeMemberAccess(arg, this.includeResultOperator.NavigationPropertyPath.Member),
                        arg));

                if (this.includeResultOperator.ChainedNavigationProperties == null)
                {
                    return methodCallExpression;
                }

                foreach (PropertyInfo inclProp in this.includeResultOperator.ChainedNavigationProperties)
                {
                    Type collElementType = Server.QueryDataHelper.GetSequenceType(toType, null);

                    IIncludableQueryable<object, object> refArg = null;
                    IIncludableQueryable<object, ICollection<object>> collArg = null;

                    MethodInfo miThenInclude;
                    Type prevType;
                    if (collElementType != null)
                    {
                        prevType = collElementType;
                        miThenInclude =
                            MethodInfoExtensions.GetMethodInfo(() => collArg.ThenInclude<object, object, object>(null));
                    }
                    else
                    {
                        prevType = toType;
                        miThenInclude =
                            MethodInfoExtensions.GetMethodInfo(() => refArg.ThenInclude<object, object, object>(null));
                    }

                    toType = inclProp.PropertyType;
                    arg = Expression.Parameter(prevType, @"x");

                    methodCallExpression = Expression.Call(
                        miThenInclude.GetGenericMethodDefinition().MakeGenericMethod(entityType, prevType, toType),
                        methodCallExpression,
                        Expression.Lambda(Expression.Property(arg, inclProp), arg));
                }

                return methodCallExpression;
            }
        }

        private class AsyncEnumerableAdapter<T> : IAsyncEnumerable<T>
        {
            private readonly Func<IAsyncEnumerator<T>> enumeratorFactory;

            public AsyncEnumerableAdapter(
                Task<IEnumerable<DynamicObject>> asyncResult,
                IDynamicObjectMapper mapper)
            {
                this.enumeratorFactory =
                    () => new AsyncEnumerator(MapResultsAsync(asyncResult, mapper));
            }

            private static async Task<IEnumerable<T>> MapResultsAsync(
                Task<IEnumerable<DynamicObject>> asyncResult,
                IDynamicObjectMapper mapper)
            {
                IEnumerable<DynamicObject> dataRecords = await asyncResult;
                if (dataRecords == null)
                {
                    return Enumerable.Repeat(default(T), 1);
                }

                return mapper.Map<T>(dataRecords);
            }

            public IAsyncEnumerator<T> GetEnumerator() => this.enumeratorFactory();

            private class AsyncEnumerator : IAsyncEnumerator<T>
            {
                private readonly Task<IEnumerable<T>> asyncResult;
                private IEnumerator<T> enumerator;

                public AsyncEnumerator(Task<IEnumerable<T>> asyncResult)
                {
                    this.asyncResult = asyncResult;
                }

                public T Current =>
                    this.enumerator == null
                    ? default(T)
                    : this.enumerator.Current;

                public void Dispose()
                {
                    this.enumerator?.Dispose();
                }

                public async Task<bool> MoveNext(CancellationToken cancellationToken)
                {
                    if (this.enumerator == null)
                    {
                        this.enumerator = (await this.asyncResult).GetEnumerator();
                    }

                    return this.enumerator.MoveNext();
                }
            }
        }
    }
}
