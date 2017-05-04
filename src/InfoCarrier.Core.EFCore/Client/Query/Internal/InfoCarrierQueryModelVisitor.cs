namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Common;
    using ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Extensions.Internal;
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
    using Remotion.Linq.Clauses.Expressions;
    using ExpressionVisitorBase = Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.ExpressionVisitorBase;

    public partial class InfoCarrierQueryModelVisitor : EntityQueryModelVisitor
    {
        private static readonly TypeInfo SubqueryInjectorClass =
            typeof(NavigationRewritingExpressionVisitor).GetTypeInfo()
                .GetDeclaredNestedType("NavigationRewritingQueryModelVisitor")
                .GetDeclaredNestedType("SubqueryInjector");

        private readonly IEntityMaterializerSource entityMaterializerSource;
        private readonly bool isRoot;

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
            QueryCompilationContext queryCompilationContext,
            bool isRoot)
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
            this.isRoot = isRoot;
        }

        private bool ExpressionIsQueryable =>
            this.Expression != null
            && this.Expression.Type.GetGenericTypeImplementations(typeof(IQueryable<>)).Any();

        private bool ExpressionIsAsyncEnumerable =>
            this.Expression != null
            && this.Expression.Type.GetGenericTypeImplementations(typeof(IAsyncEnumerable<>)).Any();

        private InfoCarrierLinqOperatorProvider InfoCarrierLinqOperatorProvider =>
            this.ExpressionIsQueryable
                ? InfoCarrierQueryableLinqOperatorProvider.Instance
                : InfoCarrierEnumerableLinqOperatorProvider.Instance;

        public override ILinqOperatorProvider LinqOperatorProvider =>
            this.ExpressionIsAsyncEnumerable
                ? (ILinqOperatorProvider)new AsyncLinqOperatorProvider()
                : this.InfoCarrierLinqOperatorProvider;

        public override void VisitQueryModel(QueryModel queryModel)
        {
            base.VisitQueryModel(queryModel);

            // CreateAsyncQueryExecutor requires the expression type to be generic
            // which is not the case since we are building synchronous LINQ expressions
            // even for async queries.
            // UGLY: apply dummy ToSequence wrapper to the expression
            if (this.isRoot && ((InfoCarrierQueryCompilationContext)this.QueryCompilationContext).Async)
            {
                this.Expression
                    = Expression.Call(
                        this.LinqOperatorProvider.ToSequence.MakeGenericMethod(this.Expression.Type),
                        this.Expression);
            }
        }

        protected override void SingleResultToSequence(QueryModel queryModel, Type type = null)
        {
            // UGLY: unapply dummy ToSequence wrapper temporarily added in VisitQueryModel
            if (this.Expression is MethodCallExpression mc
                && mc.Method.MethodIsClosedFormOf(this.LinqOperatorProvider.ToSequence))
            {
                this.Expression = mc.Arguments.Single();
            }

            base.SingleResultToSequence(queryModel, type);
        }

        private static IAsyncEnumerable<TResult> ExecuteAsyncQuery<TResult>(
            QueryContext queryContext,
            QueryCompilationContext queryCompilationContext,
            IEntityMaterializerSource entityMaterializerSource,
            Expression expression)
        {
            return new QueryExecutor<TResult>(
                queryContext, queryCompilationContext, entityMaterializerSource, expression)
                .ExecuteAsyncQuery();
        }

        private static IEnumerable<TResult> ExecuteQuery<TResult>(
            QueryContext queryContext,
            QueryCompilationContext queryCompilationContext,
            IEntityMaterializerSource entityMaterializerSource,
            Expression expression)
        {
            return new QueryExecutor<TResult>(
                queryContext, queryCompilationContext, entityMaterializerSource, expression)
                .ExecuteQuery();
        }

        protected override void TrackEntitiesInResults<TResult>(QueryModel queryModel)
        {
            // Unwrap expression (revert SingleResultToSequence)
            Expression linqExpression = this.Expression;
            if (linqExpression is MethodCallExpression call)
            {
                if (call.Method.MethodIsClosedFormOf(this.LinqOperatorProvider.ToSequence))
                {
                    linqExpression = call.Arguments.Single();
                }
            }

            // Replace ToSequence with ExecuteQuery
            MethodInfo execQueryMethod =
                ((InfoCarrierQueryCompilationContext)this.QueryCompilationContext).Async
                    ? Utils.GetMethodInfo(() => ExecuteAsyncQuery<object>(null, null, null, null))
                    : Utils.GetMethodInfo(() => ExecuteQuery<object>(null, null, null, null));

            Type resultType = Utils.TryGetQueryResultSequenceType(linqExpression.Type) ?? linqExpression.Type;

            this.Expression
                = Expression.Call(
                    execQueryMethod
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(resultType),
                    QueryContextParameter,
                    Expression.Constant(this.QueryCompilationContext),
                    Expression.Constant(this.entityMaterializerSource),
                    Expression.Constant(linqExpression));

            // Track results
            base.TrackEntitiesInResults<TResult>(queryModel);
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
            bool canUseStringIncludeOnSource
                = this.QueryCompilationContext.QueryAnnotations
                    .OfType<IncludeResultOperator>()
                    .Where(o => o.QuerySource == includeSpecification.QuerySource)
                    .Any(o => !string.IsNullOrEmpty(o.StringNavigationPropertyPath));

            // TODO: do some testing against real database.
            // IncludeExpressionVisitor may append the same .Include
            // multiple times (to QueryableStub and to Select) in some situations.
            // Need to know if it leads to bad SQL.
            var includeExpressionVisitor
                = new IncludeExpressionVisitor(
                    this.LinqOperatorProvider,
                    includeSpecification,
                    accessorExpression,
                    canUseStringIncludeOnSource);

            this.Expression = includeExpressionVisitor.Visit(this.Expression);
        }

        public override TResult BindNavigationPathPropertyExpression<TResult>(
            Expression propertyExpression,
            Func<IEnumerable<IPropertyBase>, IQuerySource, TResult> propertyBinder)
        {
            // UGLY: this is the hackiest thing I ever did! It will break if EF.Core team changes their implementation
            // https://github.com/aspnet/EntityFramework/blob/rel/1.1.0/src/Microsoft.EntityFrameworkCore/Query/ExpressionVisitors/Internal/NavigationRewritingExpressionVisitor.cs#L1233
            //
            // We check if the propertyBinder (local functor) comes from the private class
            // NavigationRewritingExpressionVisitor.NavigationRewritingQueryModelVisitor.SubqueryInjector
            // and override the logic a bit.
            if (propertyBinder.Target.GetType().DeclaringType == SubqueryInjectorClass)
            {
                propertyBinder = (properties, querySource) =>
                {
                    var navigations = properties.OfType<INavigation>().ToList();
                    var collectionNavigation = navigations.SingleOrDefault(n => n.IsCollection());
                    if (collectionNavigation == null)
                    {
                        return default(TResult);
                    }

                    // Expand collection property access into subquery (same as in EF.Core)
                    var targetType = collectionNavigation.GetTargetType().ClrType;
                    var mainFromClause = new MainFromClause(targetType.Name.Substring(0, 1).ToLowerInvariant(), targetType, propertyExpression);
                    var selector = new QuerySourceReferenceExpression(mainFromClause);
                    var subqueryModel = new QueryModel(mainFromClause, new SelectClause(selector));
                    var subqueryExpression = new SubQueryExpression(subqueryModel);

                    // Convert subquery back to ICollection (in this case to List)
                    // instead of wrapping into MaterializeCollectionNavigation method call.
                    return (TResult)(object)Expression.Call(
                        Utils.GetMethodInfo(() => Enumerable.ToList<object>(null))
                            .GetGenericMethodDefinition()
                            .MakeGenericMethod(subqueryExpression.Type.GenericTypeArguments),
                        subqueryExpression);
                };
            }

            return base.BindNavigationPathPropertyExpression(propertyExpression, propertyBinder);
        }

        private sealed class QueryExecutor<TResult> : DynamicObjectMapper
        {
            private readonly QueryContext queryContext;
            private readonly QueryCompilationContext queryCompilationContext;
            private readonly IEntityMaterializerSource entityMaterializerSource;
            private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();
            private readonly IInfoCarrierBackend infoCarrierBackend;
            private readonly Remote.Linq.Expressions.Expression rlinq;
            private readonly Aqua.TypeSystem.ITypeResolver typeResolver;

            private QueryExecutor(
                DynamicObjectMapperSettings settings,
                Aqua.TypeSystem.ITypeResolver typeResolver)
                : base(settings, typeResolver)
            {
                this.typeResolver = typeResolver;
            }

            public QueryExecutor(
                QueryContext queryContext,
                QueryCompilationContext queryCompilationContext,
                IEntityMaterializerSource entityMaterializerSource,
                Expression expression)
                : this(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true }, new Aqua.TypeSystem.TypeResolver())
            {
                this.queryContext = queryContext;
                this.queryCompilationContext = queryCompilationContext;
                this.entityMaterializerSource = entityMaterializerSource;
                this.infoCarrierBackend = ((InfoCarrierQueryContext)queryContext).InfoCarrierBackend;

                // Substitute query parameters
                expression = new SubstituteParametersExpressionVisitor(queryContext, this.queryCompilationContext.Model)
                    .Visit(expression);

                // Replace NullConditionalExpression with NullConditionalExpressionStub MethodCallExpression
                expression = Utils.ReplaceNullConditional(expression, true);

                // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
                this.rlinq = expression
                    .ToRemoteLinqExpression()
                    .ReplaceQueryableByResourceDescriptors(this.typeResolver)
                    .ReplaceGenericQueryArgumentsByNonGenericArguments();
            }

            public IEnumerable<TResult> ExecuteQuery()
            {
                IEnumerable<DynamicObject> dataRecords = this.infoCarrierBackend.QueryData(this.rlinq);
                if (dataRecords == null)
                {
                    return Enumerable.Repeat(default(TResult), 1);
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
                if (this.TryMapEntity(obj, out object entity))
                {
                    return entity;
                }

                // is obj an array
                if (this.TryMapArray(obj, targetType, out object array))
                {
                    return array;
                }

                // is obj a grouping
                if (this.TryMapGrouping(obj, targetType, out object grouping))
                {
                    return grouping;
                }

                // is targetType a collection?
                Type elementType = Utils.TryGetQueryResultSequenceType(targetType);
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

            private bool TryMapArray(object obj, Type targetType, out object array)
            {
                array = null;

                if (obj is DynamicObject dobj)
                {
                    if (dobj.Type != null)
                    {
                        // Our custom mapping of arrays doesn't contain Type
                        return false;
                    }

                    if (!dobj.TryGet("Elements", out object elements))
                    {
                        return false;
                    }

                    if (!dobj.TryGet("ArrayType", out object arrayTypeObj))
                    {
                        return false;
                    }

                    if (targetType.IsArray)
                    {
                        array = this.MapFromDynamicObjectGraph(elements, targetType);
                        return true;
                    }

                    if (arrayTypeObj is Aqua.TypeSystem.TypeInfo typeInfo)
                    {
                        array = this.MapFromDynamicObjectGraph(elements, typeInfo.Type);
                        return true;
                    }
                }

                return false;
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

                if (!dobj.TryGet("Key", out object key))
                {
                    return false;
                }

                if (!dobj.TryGet("Elements", out object elements))
                {
                    return false;
                }

                Type keyType = type.GenericTypeArguments[0];
                Type elementType = type.GenericTypeArguments[1];

                key = this.MapFromDynamicObjectGraph(key, keyType);
                elements = this.MapFromDynamicObjectGraph(elements, typeof(List<>).MakeGenericType(elementType));

                grouping = Utils.GetMethodInfo(() => MakeGenericGrouping<object, object>(null, null))
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

                if (!dobj.TryGet(Server.QueryDataHelper.EntityTypeNameTag, out object entityTypeName))
                {
                    return false;
                }

                if (!(entityTypeName is string))
                {
                    return false;
                }

                IEntityType entityType = this.queryCompilationContext.Model.FindEntityType(entityTypeName.ToString());
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
                entity = this.queryContext
                    .QueryBuffer
                    .GetEntity(
                        entityType.FindPrimaryKey(),
                        new EntityLoadInfo(
                            new ValueBuffer(scalarValues),
                            this.entityMaterializerSource.GetMaterializer(entityType)),
                        queryStateManager: this.queryCompilationContext.IsTrackingQuery,
                        throwOnNullKey: false);

                this.map.Add(dobj, entity);

                // Set navigation properties AFTER adding to map to avoid endless recursion
                foreach (INavigation navigation in entityType.GetNavigations())
                {
                    // TODO: shall we skip already loaded navigations if the entity is already tracked?
                    if (dobj.TryGet(navigation.Name, out object value) && value != null)
                    {
                        value = this.MapFromDynamicObjectGraph(value, navigation.ClrType);
                        if (navigation.IsCollection())
                        {
                            // TODO: clear or skip collection if it already contains something?
                            navigation.GetCollectionAccessor().AddRange(entity, ((IEnumerable)value).Cast<object>());
                        }
                        else
                        {
                            navigation.GetSetter().SetClrValue(entity, value);
                        }
                    }
                }

                return true;
            }

            private class SubstituteParametersExpressionVisitor : ExpressionVisitorBase
            {
                private readonly QueryContext queryContext;
                private readonly IModel model;

                public SubstituteParametersExpressionVisitor(QueryContext queryContext, IModel model)
                {
                    this.queryContext = queryContext;
                    this.model = model;
                }

                protected override Expression VisitMethodCall(MethodCallExpression node)
                {
                    Expression maybeInlineEntityProperty = this.TryVisitInlineEntityProperty(node);
                    if (maybeInlineEntityProperty != null)
                    {
                        return maybeInlineEntityProperty;
                    }

                    if (node.Method.MethodIsClosedFormOf(DefaultQueryExpressionVisitor.GetParameterValueMethodInfo))
                    {
                        Type paramType = node.Method.GetGenericArguments().Single();
                        object paramValue =
                            Utils.GetMethodInfo(() => this.GetParameterValue<object>(node))
                                .GetGenericMethodDefinition()
                                .MakeGenericMethod(paramType)
                                .ToDelegate<Func<MethodCallExpression, object>>(this)
                                .Invoke(node);

                        return Expression.Property(
                            Expression.Constant(paramValue),
                            paramValue.GetType(),
                            nameof(ValueWrapper<object>.Value));
                    }

                    return base.VisitMethodCall(node);
                }

                private Expression TryVisitInlineEntityProperty(MethodCallExpression node)
                {
                    if (!EntityQueryModelVisitor.IsPropertyMethod(node.Method))
                    {
                        return null;
                    }

                    var propertyNameExpression = node.Arguments[1] as ConstantExpression;
                    string propertyName = propertyNameExpression?.Value as string;
                    if (propertyName == null)
                    {
                        return null;
                    }

                    object entity = GetEntity();
                    object GetEntity()
                    {
                        switch (node.Arguments[0])
                        {
                            case ConstantExpression maybeConstant:
                                return maybeConstant.Value;

                            case MethodCallExpression maybeMethodCall
                            when maybeMethodCall.Method.MethodIsClosedFormOf(DefaultQueryExpressionVisitor.GetParameterValueMethodInfo):
                                return
                                    Expression.Lambda<Func<QueryContext, object>>(maybeMethodCall, EntityQueryModelVisitor.QueryContextParameter)
                                        .Compile()
                                        .Invoke(this.queryContext);

                            default:
                                return null;
                        }
                    }

                    if (entity == null)
                    {
                        return null;
                    }

                    IEntityType efType = this.model.FindEntityType(entity.GetType());
                    IProperty efProperty = efType?.FindProperty(propertyName);
                    if (efProperty == null)
                    {
                        return null;
                    }

                    object paramValue =
                        Utils.GetMethodInfo(() => Wrap<object>(null))
                            .GetGenericMethodDefinition()
                            .MakeGenericMethod(efProperty.ClrType)
                            .Invoke(null, new object[] { efProperty.GetGetter().GetClrValue(entity) });

                    Expression result = Expression.Property(
                        Expression.Constant(paramValue),
                        paramValue.GetType(),
                        nameof(ValueWrapper<object>.Value));

                    if (result.Type != node.Type)
                    {
                        result = Expression.Convert(result, node.Type);
                    }

                    return result;
                }

                private object GetParameterValue<T>(MethodCallExpression node) => Wrap(
                    Expression
                        .Lambda<Func<QueryContext, T>>(node, EntityQueryModelVisitor.QueryContextParameter)
                        .Compile()
                        .Invoke(this.queryContext));

                private static object Wrap<T>(T value) => new ValueWrapper<T> { Value = value };

                private struct ValueWrapper<T>
                {
                    public T Value { get; set; }
                }
            }
        }

        private class IncludeExpressionVisitor : ExpressionVisitorBase
        {
            private readonly IncludeSpecification includeSpecification;
            private readonly ILinqOperatorProvider linqOperatorProvider;
            private readonly Expression accessorExpression;
            private readonly bool useString;

            public IncludeExpressionVisitor(
                ILinqOperatorProvider linqOperatorProvider,
                IncludeSpecification includeSpecification,
                Expression accessorExpression,
                bool useString)
            {
                this.linqOperatorProvider = linqOperatorProvider;
                this.includeSpecification = includeSpecification;
                this.accessorExpression = accessorExpression;
                this.useString = useString;
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                Expression result = base.VisitMethodCall(node);

                if (this.IsMatchingSelect(node))
                {
                    result = this.ApplyTopLevelInclude(result);
                }

                return result;
            }

            private bool IsMatchingSelect(MethodCallExpression node)
            {
                if (!node.Method.MethodIsClosedFormOf(this.linqOperatorProvider.Select))
                {
                    return false;
                }

                var unary = node.Arguments[1] as UnaryExpression;
                if (unary == null
                    || unary.NodeType != ExpressionType.Quote
                    || unary.Operand.NodeType != ExpressionType.Lambda)
                {
                    return false;
                }

                var lambda = unary.Operand as LambdaExpression;
                return lambda != null && lambda.Body == this.accessorExpression;
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                var stub = node.Value as InfoCarrierEntityQueryableExpressionVisitor.RemoteQueryableStub;
                if (stub?.QuerySource == this.includeSpecification.QuerySource)
                {
                    return this.ApplyTopLevelInclude(node);
                }

                return base.VisitConstant(node);
            }

            private Expression ApplyTopLevelInclude(Expression node)
            {
                using (IEnumerator<INavigation> iNav = this.includeSpecification.NavigationPath.GetEnumerator())
                {
                    if (!iNav.MoveNext())
                    {
                        return node;
                    }

                    Type entityType = node.Type.GetGenericArguments().Single();

                    if (this.useString)
                    {
                        return Expression.Call(
                            Utils.GetMethodInfo(() => QueryFunctions.Include<object>(null, null))
                                .GetGenericMethodDefinition()
                                .MakeGenericMethod(entityType),
                            node,
                            Expression.Constant(string.Join(".", this.includeSpecification.NavigationPath.Select(n => n.Name))));
                    }

                    Expression BuildMemberAccessLambda(INavigation navigation, Type paramType, string paramName)
                    {
                        var arg = Expression.Parameter(paramType, paramName);
                        return Expression.Lambda(Expression.MakeMemberAccess(arg, navigation.GetMemberInfo(false, false)), arg);
                    }

                    MethodCallExpression result = Expression.Call(
                        Utils.GetMethodInfo(() => EntityFrameworkQueryableExtensions.Include<object, object>(null, null))
                            .GetGenericMethodDefinition()
                            .MakeGenericMethod(entityType, iNav.Current.ClrType),
                        node,
                        BuildMemberAccessLambda(iNav.Current, entityType, this.includeSpecification.QuerySource.ItemName));

                    for (INavigation prev = iNav.Current; iNav.MoveNext(); prev = iNav.Current)
                    {
                        MethodInfo miThenInclude =
                            prev.IsCollection()
                                ? Utils.GetMethodInfo<IIncludableQueryable<object, IEnumerable<object>>>(
                                    x => x.ThenInclude<object, object, object>(null))
                                : Utils.GetMethodInfo<IIncludableQueryable<object, object>>(
                                    x => x.ThenInclude<object, object, object>(null));

                        Type prevType = prev.GetTargetType().ClrType;

                        result = Expression.Call(
                            miThenInclude.GetGenericMethodDefinition().MakeGenericMethod(entityType, prevType, iNav.Current.ClrType),
                            result,
                            BuildMemberAccessLambda(iNav.Current, prevType, @"x"));
                    }

                    return result;
                }
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
