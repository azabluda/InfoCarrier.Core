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
    using Aqua.Dynamic;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Remote.Linq;
    using Remote.Linq.ExpressionVisitors;
    using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;

    public class InfoCarrierQueryCompiler : IQueryCompiler
    {
        private static readonly MethodInfo MakeGenericGroupingMethod
            = Utils.GetMethodInfo(() => MakeGenericGrouping<object, object>(null, null))
                .GetGenericMethodDefinition();

        private static readonly MethodInfo ExecuteQueryMethod
            = typeof(InfoCarrierQueryCompiler).GetTypeInfo().GetDeclaredMethod(nameof(ExecuteQuery));

        private static readonly IEvaluatableExpressionFilter EvaluatableExpressionFilter
            = new EvaluatableExpressionFilter();

        private readonly IQueryContextFactory queryContextFactory;
        private readonly IInterceptingLogger<LoggerCategory.Query> logger;

        public InfoCarrierQueryCompiler(
            IQueryContextFactory queryContextFactory,
            IInterceptingLogger<LoggerCategory.Query> logger)
        {
            this.queryContextFactory = queryContextFactory;
            this.logger = logger;
        }

        public Func<QueryContext, IAsyncEnumerable<TResult>> CreateCompiledAsyncEnumerableQuery<TResult>(Expression query)
            => queryContext => this.CreateQueryExecutor<TResult>(queryContext, query).ExecuteAsyncQuery();

        public Func<QueryContext, Task<TResult>> CreateCompiledAsyncTaskQuery<TResult>(Expression query)
        {
            var exec = this.CreateCompiledAsyncEnumerableQuery<TResult>(query);
            return async queryContext =>
            {
                var asyncEnumerable = exec(queryContext);
                using (var asyncEnumerator = asyncEnumerable.GetEnumerator())
                {
                    await asyncEnumerator.MoveNext(queryContext.CancellationToken);
                    return asyncEnumerator.Current;
                }
            };
        }

        public Func<QueryContext, TResult> CreateCompiledQuery<TResult>(Expression query)
        {
            Type sequenceType = Utils.TryGetQueryResultSequenceType(typeof(TResult));
            if (sequenceType == null)
            {
                return queryContext => this.ExecuteQuery<TResult>(queryContext, query).First();
            }
            else
            {
                MethodInfo exec = ExecuteQueryMethod.MakeGenericMethod(sequenceType);
                return queryContext =>
                {
                    try
                    {
                        return (TResult)exec.Invoke(this, new object[] { queryContext, query });
                    }
                    catch (TargetInvocationException ex)
                    {
                        ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                        throw;
                    }
                };
            }
        }

        private IEnumerable<TResult> ExecuteQuery<TResult>(QueryContext queryContext, Expression query)
            => this.CreateQueryExecutor<TResult>(queryContext, query).ExecuteQuery();

        private QueryExecutor<TResult> CreateQueryExecutor<TResult>(QueryContext queryContext, Expression query)
        {
            var visitor
                = new ParameterExtractingExpressionVisitor(
                    EvaluatableExpressionFilter,
                    queryContext,
                    this.logger,
                    true);

            query = visitor.ExtractParameters(query);

            return new QueryExecutor<TResult>(queryContext, query);
        }

        public TResult Execute<TResult>(Expression query)
            => this.CreateCompiledQuery<TResult>(query).Invoke(this.queryContextFactory.Create());

        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression query)
            => this.CreateCompiledAsyncEnumerableQuery<TResult>(query).Invoke(this.queryContextFactory.Create());

        public Task<TResult> ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken)
            => this.CreateCompiledAsyncTaskQuery<TResult>(query).Invoke(this.queryContextFactory.Create());

        private static IGrouping<TKey, TElement> MakeGenericGrouping<TKey, TElement>(TKey key, IEnumerable<TElement> elements)
        {
            return elements.GroupBy(x => key).Single();
        }

        private sealed class QueryExecutor<TResult> : DynamicObjectMapper
        {
            private readonly QueryContext queryContext;
            private readonly IEntityMaterializerSource entityMaterializerSource;
            private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();
            private readonly List<Action<IStateManager>> trackEntityActions = new List<Action<IStateManager>>();
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
                Expression expression)
                : this(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true }, new Aqua.TypeSystem.TypeResolver())
            {
                this.queryContext = queryContext;
                this.entityMaterializerSource = queryContext.Context.GetService<IEntityMaterializerSource>();
                this.infoCarrierBackend = ((InfoCarrierQueryContext)queryContext).InfoCarrierBackend;

                // Substitute query parameters
                expression = new SubstituteParametersExpressionVisitor(queryContext).Visit(expression);

                // Replace NullConditionalExpression with NullConditionalExpressionStub MethodCallExpression
                expression = Utils.ReplaceNullConditional(expression, true);

                expression = EntityQueryableStubVisitor.Replace(expression);

                // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
                this.rlinq = expression
                    .SimplifyIncorporationOfRemoteQueryables()
                    .ToRemoteLinqExpression()
                    .ReplaceQueryableByResourceDescriptors(this.typeResolver)
                    .ReplaceGenericQueryArgumentsByNonGenericArguments();
            }

            public IEnumerable<TResult> ExecuteQuery()
            {
                IEnumerable<DynamicObject> dataRecords = this.infoCarrierBackend.QueryData(this.rlinq);
                return this.MapAndTrackResults(dataRecords);
            }

            public IAsyncEnumerable<TResult> ExecuteAsyncQuery()
            {
                async Task<IEnumerable<TResult>> MapAndTrackResultsAsync()
                    => this.MapAndTrackResults(await this.infoCarrierBackend.QueryDataAsync(this.rlinq));

                return new AsyncEnumerableAdapter<TResult>(MapAndTrackResultsAsync());
            }

            private IEnumerable<TResult> MapAndTrackResults(IEnumerable<DynamicObject> dataRecords)
            {
                if (dataRecords == null)
                {
                    return Enumerable.Repeat(default(TResult), 1);
                }

                var result = this.Map<TResult>(dataRecords);

                foreach (var action in this.trackEntityActions)
                {
                    action(this.queryContext.StateManager);
                }

                return result;
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
                if (collType.GetTypeInfo().IsGenericType && collType.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>))
                {
                    return new LinqOperatorProvider().ToOrdered.MakeGenericMethod(collType.GenericTypeArguments)
                        .Invoke(null, new[] { list });
                }

                // materialize IQueryable<> / IOrderedQueryable<>
                if (collType.GetTypeInfo().IsGenericType
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
                    || !type.GetTypeInfo().IsGenericType
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

                grouping = MakeGenericGroupingMethod
                    .MakeGenericMethod(keyType, elementType)
                    .Invoke(null, new[] { key, elements });
                return true;
            }

            private bool TryMapEntity(object obj, out object entity)
            {
                entity = null;

                var dobj = obj as DynamicObject;
                if (dobj == null)
                {
                    return false;
                }

                if (!dobj.TryGet(@"__EntityType", out object entityTypeName))
                {
                    return false;
                }

                if (!(entityTypeName is string))
                {
                    return false;
                }

                IEntityType entityType = this.queryContext.Context.Model.FindEntityType(entityTypeName.ToString());
                if (entityType == null)
                {
                    return false;
                }

                if (this.map.TryGetValue(dobj, out entity))
                {
                    return true;
                }

                // Map only scalar properties for now, navigations must be set later
                object[] scalarValues = entityType
                    .GetProperties()
                    .Select(p => this.MapFromDynamicObjectGraph(dobj.Get(p.Name), p.ClrType))
                    .ToArray();

                // Get entity instance from EFC's identity map, or create a new one
                entity = this.queryContext
                    .QueryBuffer
                    .GetEntity(
                        entityType.FindPrimaryKey(),
                        new EntityLoadInfo(
                            new ValueBuffer(scalarValues),
                            this.entityMaterializerSource.GetMaterializer(entityType)),
                        queryStateManager: this.queryContext.Context.ChangeTracker.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll,
                        throwOnNullKey: false);

                this.map.Add(dobj, entity);
                object entityNoRef = entity;

                if (dobj.PropertyNames.Contains(@"__EntityIsTracked"))
                {
                    this.trackEntityActions.Add(stateManager =>
                    {
                        var entry = stateManager.GetOrCreateEntry(entityNoRef);
                        if (entry.EntityState == EntityState.Detached)
                        {
                            entry.ToEntityEntry().State = EntityState.Unchanged;
                        }
                    });
                }

                if (dobj.TryGet(@"__EntityLoadedNavigations", out object ln))
                {
                    var loadedNavigations = new HashSet<string>(
                        ln as IEnumerable<string> ?? Enumerable.Empty<string>());

                    this.trackEntityActions.Add(stateManager =>
                    {
                        var entry = stateManager.TryGetEntry(entityNoRef);
                        if (entry == null)
                        {
                            return;
                        }

                        foreach (INavigation nav in entry.EntityType.GetNavigations())
                        {
                            bool loaded = loadedNavigations.Contains(nav.Name);
                            if (!loaded && !nav.IsCollection() && nav.GetGetter().GetClrValue(entityNoRef) != null)
                            {
                                continue;
                            }

                            entry.SetIsLoaded(nav, loaded);
                        }
                    });
                }

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
        }

        private class SubstituteParametersExpressionVisitor : Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.ExpressionVisitorBase
        {
            private static readonly MethodInfo WrapMethod
                = Utils.GetMethodInfo(() => Wrap<object>(null)).GetGenericMethodDefinition();

            private readonly QueryContext queryContext;

            public SubstituteParametersExpressionVisitor(QueryContext queryContext)
            {
                this.queryContext = queryContext;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node.Name?.StartsWith(CompiledQueryCache.CompiledQueryParameterPrefix, StringComparison.Ordinal) == true)
                {
                    object paramValue =
                        WrapMethod
                            .MakeGenericMethod(node.Type)
                            .Invoke(null, new[] { this.queryContext.ParameterValues[node.Name] });

                    return Expression.Property(
                        Expression.Constant(paramValue),
                        paramValue.GetType(),
                        nameof(ValueWrapper<object>.Value));
                }

                return base.VisitParameter(node);
            }

            private static object Wrap<T>(T value) => new ValueWrapper<T> { Value = value };

            private struct ValueWrapper<T>
            {
                public T Value { get; set; }
            }
        }

        private class AsyncEnumerableAdapter<T> : IAsyncEnumerable<T>
        {
            private readonly Func<IAsyncEnumerator<T>> enumeratorFactory;

            public AsyncEnumerableAdapter(Task<IEnumerable<T>> asyncResult)
            {
                this.enumeratorFactory =
                    () => new AsyncEnumerator(asyncResult);
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

        private class EntityQueryableStubVisitor : ExpressionVisitorBase
        {
            private static readonly MethodInfo RemoteQueryableStubCreateMethod
                = Utils.GetMethodInfo(() => RemoteQueryableStub.Create<object>())
                    .GetGenericMethodDefinition();

            internal static Expression Replace(Expression expression)
                => new EntityQueryableStubVisitor().Visit(expression);

            protected override Expression VisitConstant(ConstantExpression constantExpression)
                => constantExpression.IsEntityQueryable()
                    ? this.VisitEntityQueryable(((IQueryable)constantExpression.Value).ElementType)
                    : constantExpression;

            private Expression VisitEntityQueryable(Type elementType)
            {
                IQueryable stub = RemoteQueryableStubCreateMethod
                    .MakeGenericMethod(elementType)
                    .ToDelegate<Func<IQueryable>>()
                    .Invoke();

                return Expression.Constant(stub);
            }

            private abstract class RemoteQueryableStub : IRemoteQueryable
            {
                public abstract Type ElementType { get; }

                protected static dynamic NotImplemented => throw new NotImplementedException();

                public Expression Expression => NotImplemented;

                public IQueryProvider Provider => NotImplemented;

                public IEnumerator GetEnumerator() => NotImplemented;

                internal static IQueryable<T> Create<T>()
                {
                    return new RemoteQueryableStub<T>();
                }
            }

            private class RemoteQueryableStub<T> : RemoteQueryableStub, IQueryable<T>
            {
                public override Type ElementType => typeof(T);

                IEnumerator<T> IEnumerable<T>.GetEnumerator() => NotImplemented;
            }
        }
    }
}
