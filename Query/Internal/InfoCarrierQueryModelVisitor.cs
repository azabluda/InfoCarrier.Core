namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Aqua.Dynamic;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Query.ResultOperators.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Modeling;
    using Remote.Linq;
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
        }

        private bool ExpressionIsQueryable =>
            this.Expression != null
            && IsQueryable(this.Expression.Type);

        internal virtual IInfoCarrierLinqOperatorProvider InfoCarrierLinqOperatorProvider =>
            this.ExpressionIsQueryable
            ? InfoCarrierQueryableLinqOperatorProvider.Instance
            : InfoCarrierEnumerableLinqOperatorProvider.Instance;

        public override ILinqOperatorProvider LinqOperatorProvider =>
            this.InfoCarrierLinqOperatorProvider;

        //public static MethodInfo ProjectionQueryMethodInfo { get; }
        //    = typeof(InfoCarrierQueryModelVisitor).GetTypeInfo()
        //        .GetDeclaredMethod(nameof(ProjectionQuery));

        public static MethodInfo EntityQueryMethodInfo { get; }
            = typeof(InfoCarrierQueryModelVisitor).GetTypeInfo()
                .GetDeclaredMethod(nameof(EntityQuery));

        //private static IEnumerable<ValueBuffer> ProjectionQuery(
        //    QueryContext queryContext,
        //    IEntityType entityType)
        //{
        //    throw new NotImplementedException();
        //    //return ((InfoCarrierQueryContext)queryContext).Store
        //    //    .GetTables(entityType)
        //    //    .SelectMany(t => t.Rows.Select(vs => new ValueBuffer(vs)));
        //}

        private static IQueryable<TEntity> EntityQuery<TEntity>(
            QueryContext queryContext,
            IModel model,
            bool queryStateManager)
            where TEntity : Entity
        {
            ServerContext sctx = ((InfoCarrierQueryContext)queryContext).ServerContext;
            Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();

            IQueryable<TEntity> qry = RemoteQueryable.Create<TEntity>(
                dataProvider: arg =>
                {
                    IInfoCarrierLogger logger = sctx.GetLogger(sctx);
                    logger.Debug("Execute query on the server");
                    logger.Debug(arg);

                    using (var xmlMsg = new ServiceMessage(sctx.SessionId, sctx.ServerPublicKey))
                    {
                        using (ServiceMessage.SecureBody bodyWriter = xmlMsg.CreateBodyWriter(sctx.ServerPublicKey))
                        {
                            RemoteLinqHelper.SaveToStream(bodyWriter.Stream, arg);
                        }

                        using (ServiceMessage cresp = sctx.ServiceDispatcher.QueryData(xmlMsg))
                        {
                            using (ServiceMessage.SecureBody bodyReader = cresp.CreateBodyReader(sctx.ClientPrivateKey))
                            {
                                return Serializer.LoadFromStream<IEnumerable<DynamicObject>>(bodyReader.Stream);
                            }
                        }
                    }
                },
                mapper: new DynamicObjectEntityMapper(
                    (obj, targetType, mapper) =>
                    {
                        foreach (DynamicObject dobj in obj.YieldAs<DynamicObject>())
                        {
                            if (!typeof(Entity).IsAssignableFrom(targetType))
                            {
                                continue;
                            }

                            object entity;
                            if (map.TryGetValue(dobj, out entity))
                            {
                                return entity;
                            }

                            IKey key = model.FindEntityType(targetType).FindPrimaryKey();

                            // TODO: may not work if the key properties are not the first, or there exists more than one key
                            var keyValueBuffer = new ValueBuffer(
                                key.Properties
                                    .Select(p => mapper.MapFromDynamicObjectGraphCustomImpl(dobj.Get(p.Name), p.ClrType))
                                    .ToList());

                            // Get/create instance of entity from EFC's identity map
                            entity = queryContext
                                .QueryBuffer
                                .GetEntity(
                                    key,
                                    new EntityLoadInfo(
                                        keyValueBuffer,
                                        vr => Activator.CreateInstance(targetType, true)),
                                    queryStateManager,
                                    throwOnNullKey: false);

                            map.Add(dobj, entity);

                            // Set entity properties
                            var targetProperties = entity.GetType().GetProperties().Where(p => p.CanWrite);
                            foreach (PropertyInfo property in targetProperties)
                            {
                                object rawValue;
                                if (dobj.TryGet(property.Name, out rawValue))
                                {
                                    object value = mapper.MapFromDynamicObjectGraphCustomImpl(rawValue, property.PropertyType);
                                    property.SetValue(entity, value);
                                }
                            }

                            return entity;
                        }

                        if (targetType.IsGenericType &&
                            targetType.GetGenericTypeDefinition() == typeof(ICollection<>))
                        {
                            return mapper.MapFromDynamicObjectGraphDefaultImpl(
                                obj,
                                typeof(List<>).MakeGenericType(targetType.GenericTypeArguments));
                        }

                        return mapper.MapFromDynamicObjectGraphDefaultImpl(obj, targetType);
                    }));

            return qry;
        }

        protected override void OptimizeQueryModel(QueryModel queryModel)
        {
            // Suppress query optimization (flattening of subqueries, etc.), because we are not going to
            // execute any statements locally. Proper optimization will take place on the server-side.
        }

        public override void VisitAdditionalFromClause(
            AdditionalFromClause fromClause,
            QueryModel queryModel,
            int index)
        {
            var fromExpression
                = CompileAdditionalFromClauseExpression(fromClause, queryModel);

            var innerItemParameter
                = Expression.Parameter(
                    GetSequenceType(fromExpression.Type), fromClause.ItemName);

            var transparentIdentifierType
                = typeof(TransparentIdentifier<,>)
                    .MakeGenericType(CurrentParameter.Type, innerItemParameter.Type);

            MethodInfo miSelectMany
                = LinqOperatorProvider.SelectMany
                    .MakeGenericMethod(
                        CurrentParameter.Type,
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
                    Expression.Lambda(firstParamDelegateType, fromExpression, CurrentParameter),
                    Expression.Lambda(
                        CallCreateTransparentIdentifier(
                            transparentIdentifierType, CurrentParameter, innerItemParameter),
                        CurrentParameter,
                        innerItemParameter));

            IntroduceTransparentScope(fromClause, queryModel, index, transparentIdentifierType);
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
                = ReplaceClauseReferences(joinClause.OuterKeySelector, joinClause);

            var innerSequenceExpression
                = CompileJoinClauseInnerSequenceExpression(joinClause, queryModel);

            var innerItemParameter
                = Expression.Parameter(
                    GetSequenceType(innerSequenceExpression.Type), joinClause.ItemName);

            if (!this.QueryCompilationContext.QuerySourceMapping.ContainsMapping(joinClause))
            {
                this.QueryCompilationContext.QuerySourceMapping
                    .AddMapping(joinClause, innerItemParameter);
            }

            var innerKeySelectorExpression
                = ReplaceClauseReferences(joinClause.InnerKeySelector, joinClause);

            var transparentIdentifierType
                = typeof(TransparentIdentifier<,>)
                    .MakeGenericType(CurrentParameter.Type, innerItemParameter.Type);

            this.Expression
                = Expression.Call(
                    LinqOperatorProvider.Join
                        .MakeGenericMethod(
                            CurrentParameter.Type,
                            innerItemParameter.Type,
                            outerKeySelectorExpression.Type,
                            transparentIdentifierType),
                    this.Expression,
                    innerSequenceExpression,
                    Expression.Lambda(outerKeySelectorExpression, CurrentParameter),
                    Expression.Lambda(innerKeySelectorExpression, innerItemParameter),
                    Expression.Lambda(
                        CallCreateTransparentIdentifier(
                            transparentIdentifierType,
                            CurrentParameter,
                            innerItemParameter),
                        CurrentParameter,
                        innerItemParameter));

            IntroduceTransparentScope(joinClause, queryModel, index, transparentIdentifierType);
        }

        protected override void IncludeNavigations(QueryModel queryModel)
        {
            if (queryModel.GetOutputDataInfo() is Remotion.Linq.Clauses.StreamedData.StreamedScalarValueInfo)
            {
                return;
            }

            foreach (var incl in this.QueryCompilationContext.QueryAnnotations.OfType<IncludeResultOperator>())
            {
                this.Expression = new InfoCarrierIncludeExpressionVisitor(incl).Visit(this.Expression);
            }

            // Although we have already added .Include to this.Expression, we have to call the base implementation
            // which will perform QueryCompilationContext.AddTrackableInclude. To avoid NotImplementedException we
            // have to override another overload of IncludeNavigations with empty body. This is a bit unclean.
            base.IncludeNavigations(queryModel);
        }

        protected override void IncludeNavigations(
            IncludeSpecification includeSpecification,
            Type resultType,
            Expression accessorExpression,
            bool querySourceRequiresTracking)
        {
            // EMPTY: see comment above
        }

        // Simplified version of
        // https://github.com/aspnet/EntityFramework/blob/1.0.0/src/Shared/SharedTypeExtensions.cs#L97
        private static Type GetSequenceType(Type type) =>
            (type.IsInterface ? type.Yield() : type.GetInterfaces())
                .SingleOrDefault(
                    i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))?
                .GenericTypeArguments.Single();

        private static bool IsQueryable(Type type)
        {
            if (type.IsInterface
                && type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                return true;
            }

            return type.GetInterfaces().Any(IsQueryable);
        }

        private sealed class DynamicObjectEntityMapper : DynamicObjectMapper
        {
            private readonly Func<object, Type, DynamicObjectEntityMapper, object> materializer;

            public DynamicObjectEntityMapper(Func<object, Type, DynamicObjectEntityMapper, object> materializer)
                : base(formatPrimitiveTypesAsString: true)
            {
                this.materializer = materializer;
            }

            public object MapFromDynamicObjectGraphCustomImpl(object obj, Type targetType)
            {
                return this.MapFromDynamicObjectGraph(obj, targetType);
            }

            public object MapFromDynamicObjectGraphDefaultImpl(object obj, Type targetType)
            {
                return base.MapFromDynamicObjectGraph(obj, targetType);
            }

            protected override object MapFromDynamicObjectGraph(object obj, Type targetType)
            {
                return this.materializer(obj, targetType, this);
            }
        }

        private class InfoCarrierIncludeExpressionVisitor : ExpressionVisitorBase
        {
            private readonly IncludeResultOperator includeResultOperator;

            public InfoCarrierIncludeExpressionVisitor(IncludeResultOperator includeResultOperator)
            {
                this.includeResultOperator = includeResultOperator;
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.MethodIsClosedFormOf(EntityQueryMethodInfo))
                {
                    return this.ApplyTopLevelInclude(node);
                }

                // TODO: apply Include to Select and OfType nodes
                return base.VisitMethodCall(node);
            }

            private Expression ApplyTopLevelInclude(MethodCallExpression methodCallExpression)
            {
                Type entityType = methodCallExpression.Type.GetGenericArguments().First();
                Type toType = this.includeResultOperator.NavigationPropertyPath.Type;

                var arg = Expression.Parameter(entityType, "x");

                methodCallExpression = Expression.Call(
                    SymbolExtensions.GetMethodInfo(() => EntityFrameworkQueryableExtensions.Include<object, object>(null, null))
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(entityType, toType),
                    methodCallExpression,
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

                    methodCallExpression = Expression.Call(
                        miThenInclude.GetGenericMethodDefinition().MakeGenericMethod(entityType, prevType, toType),
                        methodCallExpression,
                        Expression.Lambda(Expression.Property(arg, inclProp), arg));
                }

                return methodCallExpression;
            }
        }
    }
}
