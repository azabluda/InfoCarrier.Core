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
            IQuerySource querySource,
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

                            IEntityType entityType = model.FindEntityType(targetType);
                            IKey key = entityType.FindPrimaryKey();

                            // TRICKY: We need ValueBuffer containing only key values (for entity identity lookup)
                            // and shadow property values for InternalMixedEntityEntry.
                            // We will set other properties with our own algorithm.
                            var keyAndShadowProps = entityType.GetProperties().Where(p => p.IsKey() || p.IsShadowProperty).ToList();
                            var nulls = Enumerable.Repeat<object>(null, 1 + keyAndShadowProps.Select(p => p.GetIndex()).DefaultIfEmpty(-1).Max());
                            var valueBuffer = new ValueBuffer(nulls.ToList());
                            foreach (IProperty p in keyAndShadowProps)
                            {
                                valueBuffer[p.GetIndex()] = mapper.MapFromDynamicObjectGraphCustomImpl(dobj.Get(p.Name), p.ClrType);
                            }

                            // Get/create instance of entity from EFC's identity map
                            bool createdNew = false;
                            entity = queryContext
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
                                    queryStateManager,
                                    throwOnNullKey: false);

                            map.Add(dobj, entity);

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

        private static Type GetSequenceType(Type type)
        {
            Type result = Aqua.TypeSystem.TypeHelper.GetElementType(type);
            return result == type ? null : result;
        }

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
                    var querySource = ((ConstantExpression)node.Arguments[0]).Value as IQuerySource;
                    if (querySource != null
                        && querySource == this.includeResultOperator.QuerySource)
                    {
                        return this.ApplyTopLevelInclude(node);
                    }
                }

                // TODO: apply Include to Select and OfType nodes
                return base.VisitMethodCall(node);
            }

            private Expression ApplyTopLevelInclude(MethodCallExpression methodCallExpression)
            {
                Type entityType = methodCallExpression.Type.GetGenericArguments().First();
                Type toType = this.includeResultOperator.NavigationPropertyPath.Type;

                var arg = Expression.Parameter(entityType, this.includeResultOperator.QuerySource.ItemName);

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
    }
}
