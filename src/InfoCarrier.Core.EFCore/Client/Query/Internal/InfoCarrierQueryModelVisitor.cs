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
    using Remote.Linq.DynamicQuery;
    using Remote.Linq.ExpressionVisitors;
    using Remotion.Linq;
    using Remotion.Linq.Clauses;

    public partial class InfoCarrierQueryModelVisitor : EntityQueryModelVisitor
    {
        private readonly IEntityMaterializerSource entityMaterializerSource;

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
                queryOptimizer,
                navigationRewritingExpressionVisitorFactory,
                subQueryMemberPushDownExpressionVisitor,
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
            this.entityMaterializerSource = entityMaterializerSource;
        }

        private bool ExpressionIsQueryable =>
            this.Expression != null
            && this.Expression.Type.GetGenericTypeImplementations(typeof(IQueryable<>)).Any();

        private bool ExpressionIsAsyncEnumerable =>
            this.Expression != null
            && this.Expression.Type.GetGenericTypeImplementations(typeof(IAsyncEnumerable<>)).Any();

        internal virtual InfoCarrierLinqOperatorProvider InfoCarrierLinqOperatorProvider =>
            this.ExpressionIsQueryable
                ? InfoCarrierQueryableLinqOperatorProvider.Instance
                : InfoCarrierEnumerableLinqOperatorProvider.Instance;

        public override ILinqOperatorProvider LinqOperatorProvider =>
            this.ExpressionIsAsyncEnumerable
                ? (ILinqOperatorProvider)new AsyncLinqOperatorProvider()
                : this.InfoCarrierLinqOperatorProvider;

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
            IEntityMaterializerSource entityMaterializerSource,
            Expression expression,
            bool queryStateManager)
        {
            return new QueryExecutor<TResult>(queryContext, entityMaterializerSource, expression, queryStateManager)
                .ExecuteAsyncQuery();
        }

        private static IEnumerable<TResult> ExecuteQuery<TResult>(
            QueryContext queryContext,
            IEntityMaterializerSource entityMaterializerSource,
            Expression expression,
            bool queryStateManager)
        {
            return new QueryExecutor<TResult>(queryContext, entityMaterializerSource, expression, queryStateManager)
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
                    ? MethodInfoExtensions.GetMethodInfo(() => ExecuteAsyncQuery<object>(null, null, null, false))
                    : MethodInfoExtensions.GetMethodInfo(() => ExecuteQuery<object>(null, null, null, false));

            Type resultType = Server.QueryDataHelper.GetSequenceType(this.Expression.Type, this.Expression.Type);

            this.Expression
                = Expression.Call(
                    execQueryMethod
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(resultType),
                    QueryContextParameter,
                    Expression.Constant(this.entityMaterializerSource),
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
            // Also CallCreateTransparentIdentifierLambda ensures uniqueness of TransparentIdentifier lambda parameters.
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
                    CallCreateTransparentIdentifierLambda(
                        transparentIdentifierType,
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
            // Also CallCreateTransparentIdentifierLambda ensures uniqueness of TransparentIdentifier lambda parameters.
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
                    CallCreateTransparentIdentifierLambda(
                        transparentIdentifierType,
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
            // Also CallCreateTransparentIdentifierLambda ensures uniqueness of TransparentIdentifier lambda parameters.
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
                    CallCreateTransparentIdentifierLambda(
                        transparentIdentifierType,
                        this.CurrentParameter,
                        innerItemsParameter));

            this.IntroduceTransparentScope(groupJoinClause, queryModel, index, transparentIdentifierType);
        }

        private static LambdaExpression CallCreateTransparentIdentifierLambda(
            Type transparentIdentifierType,
            ParameterExpression outerParameter,
            ParameterExpression innerParameter)
        {
            var uniqueInnerParameter =
                innerParameter.Name == outerParameter.Name
                    ? Expression.Parameter(innerParameter.Type, innerParameter.Name + @"_")
                    : innerParameter;

            return Expression.Lambda(
                CallCreateTransparentIdentifier(
                    transparentIdentifierType,
                    outerParameter,
                    uniqueInnerParameter),
                outerParameter,
                uniqueInnerParameter);
        }

        protected override void IncludeNavigations(
            IncludeSpecification includeSpecification,
            Type resultType,
            Expression accessorExpression,
            bool querySourceRequiresTracking)
        {
            // Everything is already in-place so we just stub this method with empty body
        }

        private sealed class QueryExecutor<TResult> : DynamicObjectMapper
        {
            private readonly QueryContext queryContext;
            private readonly IEntityMaterializerSource entityMaterializerSource;
            private readonly bool queryStateManager;
            private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();
            private readonly IInfoCarrierBackend infoCarrierBackend;
            private readonly Remote.Linq.Expressions.Expression rlinq;

            public QueryExecutor(
                QueryContext queryContext,
                IEntityMaterializerSource entityMaterializerSource,
                Expression expression,
                bool queryStateManager)
                : base(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true })
            {
                this.queryContext = queryContext;
                this.entityMaterializerSource = entityMaterializerSource;
                this.queryStateManager = queryStateManager;
                this.infoCarrierBackend = ((InfoCarrierQueryContext)queryContext).InfoCarrierBackend;

                // Substitute query parameters
                expression = new SubstituteParametersExpressionVisitor(queryContext).Visit(expression);

                // Replace NullConditionalExpression with NullConditionalExpressionStub MethodCallExpression
                expression = new ReplaceNullConditionalExpressionVisitor(true).Visit(expression);

                // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
                this.rlinq = expression
                    .ToRemoteLinqExpression()
                    .ReplaceQueryableByResourceDescriptors()
                    .ReplaceGenericQueryArgumentsByNonGenericArguments();
            }

            private IModel Model => this.queryContext.StateManager.Value.Context.Model;

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
                Func<object> baseImpl = () => base.MapFromDynamicObjectGraph(obj, targetType);

                // mapping required?
                if (obj == null || targetType == obj.GetType())
                {
                    return baseImpl();
                }

                // is obj an entity?
                object entity;
                if (this.TryMapEntity(obj, out entity))
                {
                    return entity;
                }

                // is obj a grouping
                object grouping;
                if (this.TryMapGrouping(obj, targetType, out grouping))
                {
                    return grouping;
                }

                // is targetType a collection?
                Type elementType = Server.QueryDataHelper.GetSequenceType(targetType, null);
                if (elementType == null)
                {
                    return baseImpl();
                }

                // map to list (supported directly by aqua-core)
                Type listType = typeof(List<>).MakeGenericType(elementType);
                object list = base.MapFromDynamicObjectGraph(obj, listType) ?? Activator.CreateInstance(listType);

                // determine concrete collection type
                Type collType = new CollectionTypeFactory().TryFindTypeToInstantiate(elementType, targetType) ?? targetType;
                if (listType == collType)
                {
                    return list; // no further mapping required
                }

                // materialize IOrderedEnumerable<>
                if (collType.IsGenericType && collType.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
                {
                    return new LinqOperatorProvider().ToOrdered.MakeGenericMethod(collType.GenericTypeArguments)
                        .Invoke(null, new[] { list });
                }

                // materialize IQueryable<> / IOrderedQueryable<>
                if (collType.IsGenericType
                    && (collType.GetGenericTypeDefinition() == typeof(IQueryable<>)
                        || collType.GetGenericTypeDefinition() == typeof(IOrderedQueryable<>)))
                {
                    return new LinqOperatorProvider().ToQueryable.MakeGenericMethod(collType.GenericTypeArguments)
                        .Invoke(null, new[] { list, this.queryContext });
                }

                // materialize concrete collection
                return Activator.CreateInstance(collType, list);
            }

            private bool TryMapGrouping(object obj, Type targetType, out object grouping)
            {
                grouping = null;

                var dobj = obj as DynamicObject;
                if (dobj == null)
                {
                    return false;
                }

                Type type = dobj.Type?.Type ?? targetType;

                if (type == null
                    || !type.IsGenericType
                    || type.GetGenericTypeDefinition() != typeof(IGrouping<,>))
                {
                    return false;
                }

                object key;
                if (!dobj.TryGet("Key", out key))
                {
                    return false;
                }

                object elements;
                if (!dobj.TryGet("Elements", out elements))
                {
                    return false;
                }

                Type keyType = type.GenericTypeArguments[0];
                Type elementType = type.GenericTypeArguments[1];

                key = this.MapFromDynamicObjectGraph(key, keyType);
                elements = this.MapFromDynamicObjectGraph(elements, typeof(List<>).MakeGenericType(elementType));

                grouping = MethodInfoExtensions.GetMethodInfo(() => MakeGenericGrouping<object, object>(null, null))
                    .GetGenericMethodDefinition()
                    .MakeGenericMethod(keyType, elementType)
                    .Invoke(null, new[] { key, elements });
                return true;
            }

            private static IGrouping<TKey, TElement> MakeGenericGrouping<TKey, TElement>(TKey key, IEnumerable<TElement> elements)
            {
                return elements.GroupBy(x => key).Single();
            }

            private bool TryMapEntity(object obj, out object entity)
            {
                entity = null;

                var dobj = obj as DynamicObject;
                if (dobj == null)
                {
                    return false;
                }

                object entityTypeName;
                if (!dobj.TryGet(Server.QueryDataHelper.EntityTypeNameTag, out entityTypeName))
                {
                    return false;
                }

                if (!(entityTypeName is string))
                {
                    return false;
                }

                IEntityType entityType = this.Model.FindEntityType(entityTypeName.ToString());
                if (entityType == null)
                {
                    return false;
                }

                if (this.map.TryGetValue(dobj, out entity))
                {
                    return true;
                }

                // Map only scalar properties for now, navigations must be set later
                IList<object> scalarValues = entityType
                    .GetProperties()
                    .Select(p => this.MapFromDynamicObjectGraph(dobj.Get(p.Name), p.ClrType))
                    .ToList();

                // Get entity instance from EFC's identity map, or create a new one
                Func<ValueBuffer, object> materializer = this.entityMaterializerSource.GetMaterializer(entityType);
                entity = this.queryContext
                    .QueryBuffer
                    .GetEntity(
                        entityType.FindPrimaryKey(),
                        new EntityLoadInfo(
                            new ValueBuffer(scalarValues),
                            vr =>
                            {
                                object newEntity = materializer(vr);
                                this.map.Add(dobj, newEntity);
                                return newEntity;
                            }),
                        this.queryStateManager,
                        throwOnNullKey: false);

                // Set navigation properties AFTER adding to map to avoid endless recursion
                foreach (INavigation navigation in entityType.GetNavigations())
                {
                    // TODO: shall we skip already loaded navigations if the entity is already tracked?
                    object value;
                    if (dobj.TryGet(navigation.Name, out value))
                    {
                        value = this.MapFromDynamicObjectGraph(value, navigation.ClrType);
                        navigation.GetSetter().SetClrValue(entity, value);
                    }
                }

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

            private static Expression BindNavigationPropertyPath(ParameterExpression arg, MemberExpression navigationPropertyPath)
            {
                if (navigationPropertyPath == null)
                {
                    return arg;
                }

                return Expression.MakeMemberAccess(
                    BindNavigationPropertyPath(arg, navigationPropertyPath.Expression as MemberExpression),
                    navigationPropertyPath.Member);
            }

            private Expression ApplyTopLevelInclude(ConstantExpression constantExpression)
            {
                Type entityType = constantExpression.Type.GetGenericArguments().Single();

                if (!string.IsNullOrEmpty(this.includeResultOperator.StringNavigationPropertyPath))
                {
                    return Expression.Call(
                        MethodInfoExtensions.GetMethodInfo(() => QueryFunctions.Include<object>(null, null))
                            .GetGenericMethodDefinition()
                            .MakeGenericMethod(entityType),
                        constantExpression,
                        Expression.Constant(this.includeResultOperator.StringNavigationPropertyPath));
                }

                Type toType = this.includeResultOperator.NavigationPropertyPath.Type;

                var arg = Expression.Parameter(entityType, this.includeResultOperator.QuerySource.ItemName);

                var methodCallExpression = Expression.Call(
                    MethodInfoExtensions.GetMethodInfo(() => EntityFrameworkQueryableExtensions.Include<object, object>(null, null))
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(entityType, toType),
                    constantExpression,
                    Expression.Lambda(
                        BindNavigationPropertyPath(arg, this.includeResultOperator.NavigationPropertyPath),
                        arg));

                if (this.includeResultOperator.ChainedNavigationProperties == null)
                {
                    return methodCallExpression;
                }

                foreach (PropertyInfo inclProp in this.includeResultOperator.ChainedNavigationProperties)
                {
                    MethodInfo miThenInclude;
                    Type prevType;
                    Type collElementType = toType.TryGetElementType(typeof(IEnumerable<>));
                    if (collElementType != null)
                    {
                        prevType = collElementType;
                        miThenInclude =
                            MethodInfoExtensions.GetMethodInfo<IIncludableQueryable<object, IEnumerable<object>>>(
                                x => x.ThenInclude<object, object, object>(null));
                    }
                    else
                    {
                        prevType = toType;
                        miThenInclude =
                            MethodInfoExtensions.GetMethodInfo<IIncludableQueryable<object, object>>(
                                x => x.ThenInclude<object, object, object>(null));
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
