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
    using Common;
    using ExpressionVisitors;
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
    using Utils;

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
                queryOptimizer,
                new SuppressNavigationRewritingFactory(), // Suppress navigation rewriting
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
        }

        private bool ExpressionIsQueryable =>
            this.Expression != null
            && ImplementsGenericInterface(this.Expression.Type, typeof(IQueryable<>));

        private bool ExpressionIsAsyncEnumerable =>
            this.Expression != null
            && ImplementsGenericInterface(this.Expression.Type, typeof(IAsyncEnumerable<>));

        internal virtual IInfoCarrierLinqOperatorProvider InfoCarrierLinqOperatorProvider =>
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

        private static Remote.Linq.Expressions.Expression TranslateToRlinq<TResult>(
            QueryContext queryContext,
            Expression expression)
        {
            // Substitute query parameters
            expression = new SubstituteParametersExpressionVisitor(queryContext).Visit(expression);

            // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
            // but allows for async execution of query
            var rlinq = expression
                .ToRemoteLinqExpression()
                .ReplaceQueryableByResourceDescriptors()
                .ReplaceGenericQueryArgumentsByNonGenericArguments();
            return rlinq;
        }

        private static async Task<IEnumerable<DynamicObject>> QueryDataAsync(
            ServerContext serverContext,
            Remote.Linq.Expressions.Expression rlinq)
        {
            IInfoCarrierLogger logger = serverContext.GetLogger(serverContext);
            logger.Debug("Execute asynchronous query on the server");
            logger.Debug(rlinq);

            using (var xmlMsg = new ServiceMessage(serverContext.SessionId, serverContext.ServerPublicKey))
            {
                using (ServiceMessage.SecureBody bodyWriter = xmlMsg.CreateBodyWriter(serverContext.ServerPublicKey))
                {
                    RemoteLinqHelper.SaveToStream(bodyWriter.Stream, rlinq, serverContext.ModuleManager.DataContractSerializer);
                }

                using (ServiceMessage cresp = await serverContext.ServiceDispatcher.QueryDataAsync(xmlMsg))
                {
                    using (ServiceMessage.SecureBody bodyReader = cresp.CreateBodyReader(serverContext.ClientPrivateKey))
                    {
                        return Serializer.LoadFromStream<IEnumerable<DynamicObject>>(bodyReader.Stream);
                    }
                }
            }
        }

        private static IEnumerable<DynamicObject> QueryData(
            ServerContext serverContext,
            Remote.Linq.Expressions.Expression rlinq)
        {
            IInfoCarrierLogger logger = serverContext.GetLogger(serverContext);
            logger.Debug("Execute synchronous query on the server");
            logger.Debug(rlinq);

            using (var xmlMsg = new ServiceMessage(serverContext.SessionId, serverContext.ServerPublicKey))
            {
                using (ServiceMessage.SecureBody bodyWriter = xmlMsg.CreateBodyWriter(serverContext.ServerPublicKey))
                {
                    RemoteLinqHelper.SaveToStream(bodyWriter.Stream, rlinq, serverContext.ModuleManager.DataContractSerializer);
                }

                using (ServiceMessage cresp = serverContext.ServiceDispatcher.QueryData(xmlMsg))
                {
                    using (ServiceMessage.SecureBody bodyReader = cresp.CreateBodyReader(serverContext.ClientPrivateKey))
                    {
                        return Serializer.LoadFromStream<IEnumerable<DynamicObject>>(bodyReader.Stream);
                    }
                }
            }
        }

        private static IAsyncEnumerable<TResult> ExecuteAsyncQuery<TResult>(
            QueryContext queryContext,
            Expression expression,
            bool queryStateManager)
        {
            var rlinq = TranslateToRlinq<TResult>(queryContext, expression);
            return new AsyncEnumerableAdapter<TResult>(
                QueryDataAsync(((InfoCarrierQueryContext)queryContext).ServerContext, rlinq),
                new DynamicObjectEntityMapper(queryContext, queryStateManager));
        }

        private static IEnumerable<TResult> ExecuteQuery<TResult>(
            QueryContext queryContext,
            Expression expression,
            bool queryStateManager)
        {
            var rlinq = TranslateToRlinq<TResult>(queryContext, expression);
            IEnumerable<DynamicObject> dataRecords = QueryData(((InfoCarrierQueryContext)queryContext).ServerContext, rlinq);
            return dataRecords == null
                ? Enumerable.Empty<TResult>()
                : new DynamicObjectEntityMapper(queryContext, queryStateManager).Map<TResult>(dataRecords);
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
                    ? SymbolExtensions.GetMethodInfo(() => ExecuteAsyncQuery<object>(null, null, false))
                    : SymbolExtensions.GetMethodInfo(() => ExecuteQuery<object>(null, null, false));

            this.Expression
                = Expression.Call(
                    execQueryMethod
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(Aqua.TypeSystem.TypeHelper.GetElementType(this.Expression.Type)),
                    QueryContextParameter,
                    Expression.Constant(this.Expression),
                    Expression.Constant(this.QueryCompilationContext.IsTrackingQuery));
        }

        public override void VisitAdditionalFromClause(
            AdditionalFromClause fromClause,
            QueryModel queryModel,
            int index)
        {
            var fromExpression
                = this.CompileAdditionalFromClauseExpression(fromClause, queryModel);

            var innerItemParameter
                = Expression.Parameter(
                    GetSequenceType(fromExpression.Type), fromClause.ItemName);

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
            var outerKeySelectorExpression
                = this.ReplaceClauseReferences(joinClause.OuterKeySelector, joinClause);

            var innerSequenceExpression
                = this.CompileJoinClauseInnerSequenceExpression(joinClause, queryModel);

            var innerItemParameter
                = Expression.Parameter(
                    GetSequenceType(innerSequenceExpression.Type), joinClause.ItemName);

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
            var outerKeySelectorExpression
                = this.ReplaceClauseReferences(groupJoinClause.JoinClause.OuterKeySelector, groupJoinClause);

            var innerSequenceExpression
                = this.CompileGroupJoinInnerSequenceExpression(groupJoinClause, queryModel);

            var innerItemParameter
                = Expression.Parameter(
                    GetSequenceType(innerSequenceExpression.Type),
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

        private static Type GetSequenceType(Type type)
        {
            Type result = Aqua.TypeSystem.TypeHelper.GetElementType(type);
            return result == type ? null : result;
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

        private sealed class DynamicObjectEntityMapper : DynamicObjectMapper
        {
            private readonly QueryContext queryContext;
            private readonly bool queryStateManager;
            private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();

            public DynamicObjectEntityMapper(QueryContext queryContext, bool queryStateManager)
                : base(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true })
            {
                this.queryContext = queryContext;
                this.queryStateManager = queryStateManager;
            }

            protected override object MapFromDynamicObjectGraph(object obj, Type targetType)
            {
                foreach (DynamicObject dobj in obj.YieldAs<DynamicObject>())
                {
                    if (!typeof(Entity).IsAssignableFrom(targetType))
                    {
                        continue;
                    }

                    object entity;
                    if (this.map.TryGetValue(dobj, out entity))
                    {
                        return entity;
                    }

                    IEntityType entityType = this.queryContext.StateManager.Context.Model.FindEntityType(targetType);
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

                    // Get/create instance of entity from EFC's identity map
                    bool createdNew = false;
                    entity = this.queryContext
                        .QueryBuffer
                        .GetEntity(
                            key,
                            new EntityLoadInfo(
                                valueBuffer,
                                vr =>
                                {
                                    createdNew = true;
                                    return Activator.CreateInstance(targetType);
                                }),
                            this.queryStateManager,
                            throwOnNullKey: false);

                    this.map.Add(dobj, entity);

                    if (!createdNew)
                    {
                        return entity;
                    }

                    // Set entity properties
                    var targetProperties = entity.GetType().GetProperties().Where(p => p.CanWrite);
                    foreach (PropertyInfo property in targetProperties)
                    {
                        object rawValue;
                        if (dobj.TryGet(property.Name, out rawValue))
                        {
                            object value = this.MapFromDynamicObjectGraph(rawValue, property.PropertyType);
                            property.SetValue(entity, value);
                        }
                    }

                    return entity;
                }

                if (targetType.IsGenericType &&
                    targetType.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    return base.MapFromDynamicObjectGraph(
                        obj,
                        typeof(List<>).MakeGenericType(targetType.GenericTypeArguments));
                }

                return base.MapFromDynamicObjectGraph(obj, targetType);
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
                if (node.Value
                        .YieldAs<InfoCarrierEntityQueryableExpressionVisitor.RemoteQueryableStub>()
                        .Any(dummyQueryable => dummyQueryable.QuerySource == this.includeResultOperator.QuerySource))
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
                    SymbolExtensions.GetMethodInfo(() => EntityFrameworkQueryableExtensions.Include<object, object>(null, null))
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
                    Type collElementType = GetSequenceType(toType);

                    IIncludableQueryable<object, object> refArg = null;
                    IIncludableQueryable<object, ICollection<object>> collArg = null;

                    MethodInfo miThenInclude;
                    Type prevType;
                    if (collElementType != null)
                    {
                        prevType = collElementType;
                        miThenInclude =
                            SymbolExtensions.GetMethodInfo(() => collArg.ThenInclude<object, object, object>(null));
                    }
                    else
                    {
                        prevType = toType;
                        miThenInclude =
                            SymbolExtensions.GetMethodInfo(() => refArg.ThenInclude<object, object, object>(null));
                    }

                    toType = inclProp.PropertyType;
                    arg = Expression.Parameter(prevType, "x");

                    methodCallExpression = Expression.Call(
                        miThenInclude.GetGenericMethodDefinition().MakeGenericMethod(entityType, prevType, toType),
                        methodCallExpression,
                        Expression.Lambda(Expression.Property(arg, inclProp), arg));
                }

                return methodCallExpression;
            }
        }

        private class SuppressNavigationRewritingFactory : INavigationRewritingExpressionVisitorFactory
        {
            public NavigationRewritingExpressionVisitor Create(EntityQueryModelVisitor queryModelVisitor)
            {
                return new SuppressNavigationRewriting(queryModelVisitor);
            }
        }

        private class SuppressNavigationRewriting : NavigationRewritingExpressionVisitor
        {
            public SuppressNavigationRewriting(EntityQueryModelVisitor queryModelVisitor)
                : base(queryModelVisitor)
            {
            }

            public override void Rewrite(QueryModel queryModel)
            {
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
                return dataRecords == null
                    ? default(T).Yield()
                    : mapper.Map<T>(dataRecords);
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
