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
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Remote.Linq;
    using Remote.Linq.ExpressionVisitors;
    using MethodInfo = System.Reflection.MethodInfo;

    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1601:PartialElementsMustBeDocumented", Justification = "Reviewed.")]
    public partial class InfoCarrierQueryCompiler
    {
        private sealed class PreparedQuery
        {
            private static readonly MethodInfo MakeGenericGroupingMethod
                = Utils.GetMethodInfo(() => MakeGenericGrouping<object, object>(null, null))
                    .GetGenericMethodDefinition();

            private readonly IReadOnlyDictionary<string, IEntityType> entityTypeMap;

            public PreparedQuery(Expression expression, IReadOnlyDictionary<string, IEntityType> entityTypeMap)
            {
                this.entityTypeMap = entityTypeMap;

                // Replace NullConditionalExpression with NullConditionalExpressionStub MethodCallExpression
                expression = Utils.ReplaceNullConditional(expression, true);

                // Replace EntityQueryable with stub
                expression = EntityQueryableStubVisitor.Replace(expression);

                this.Expression = expression.SimplifyIncorporationOfRemoteQueryables();
            }

            private Expression Expression { get; }

            private ITypeResolver TypeResolver { get; } = new TypeResolver();

            public IEnumerable<TResult> Execute<TResult>(QueryContext queryContext)
                => new QueryExecutor<TResult>(this, queryContext, this.entityTypeMap).ExecuteQuery();

            public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(QueryContext queryContext)
                => new QueryExecutor<TResult>(this, queryContext, this.entityTypeMap).ExecuteAsyncQuery();

            private static IGrouping<TKey, TElement> MakeGenericGrouping<TKey, TElement>(TKey key, IEnumerable<TElement> elements)
            {
                return elements.GroupBy(x => key).Single();
            }

            private sealed class QueryExecutor<TResult> : DynamicObjectMapper
            {
                private readonly QueryContext queryContext;
                private readonly ITypeResolver typeResolver;
                private readonly IReadOnlyDictionary<string, IEntityType> entityTypeMap;
                private readonly IEntityMaterializerSource entityMaterializerSource;
                private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();
                private readonly List<Action<IStateManager>> trackEntityActions = new List<Action<IStateManager>>();
                private readonly IInfoCarrierBackend infoCarrierBackend;
                private readonly Remote.Linq.Expressions.Expression rlinq;

                public QueryExecutor(
                    PreparedQuery preparedQuery,
                    QueryContext queryContext,
                    IReadOnlyDictionary<string, IEntityType> entityTypeMap)
                    : base(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true }, preparedQuery.TypeResolver)
                {
                    this.queryContext = queryContext;
                    this.typeResolver = preparedQuery.TypeResolver;
                    this.entityTypeMap = entityTypeMap;
                    this.entityMaterializerSource = queryContext.Context.GetService<IEntityMaterializerSource>();
                    this.infoCarrierBackend = ((InfoCarrierQueryContext)queryContext).InfoCarrierBackend;

                    Expression expression = preparedQuery.Expression;

                    // Substitute query parameters
                    expression = new SubstituteParametersExpressionVisitor(queryContext).Visit(expression);

                    // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
                    this.rlinq = expression
                        .ToRemoteLinqExpression(Remote.Linq.EntityFrameworkCore.ExpressionEvaluator.CanBeEvaluated)
                        .ReplaceQueryableByResourceDescriptors(this.typeResolver)
                        .ReplaceGenericQueryArgumentsByNonGenericArguments();
                }

                public IEnumerable<TResult> ExecuteQuery()
                {
                    QueryDataResult result = this.infoCarrierBackend.QueryData(
                        new QueryDataRequest(
                            this.rlinq,
                            this.queryContext.Context.ChangeTracker.QueryTrackingBehavior),
                        this.queryContext.Context);
                    return this.MapAndTrackResults(result.MappedResults);
                }

                public IAsyncEnumerable<TResult> ExecuteAsyncQuery()
                {
                    async Task<IEnumerable<TResult>> MapAndTrackResultsAsync()
                    {
                        QueryDataResult result = await this.infoCarrierBackend.QueryDataAsync(
                            new QueryDataRequest(
                                this.rlinq,
                                this.queryContext.Context.ChangeTracker.QueryTrackingBehavior),
                            this.queryContext.Context,
                            this.queryContext.CancellationToken);
                        return this.MapAndTrackResults(result.MappedResults);
                    }

                    return new AsyncEnumerableAdapter<TResult>(MapAndTrackResultsAsync());
                }

                private IEnumerable<TResult> MapAndTrackResults(IEnumerable<DynamicObject> dataRecords)
                {
                    if (dataRecords == null)
                    {
                        return Enumerable.Repeat(default(TResult), 1);
                    }

                    var result = this.Map<TResult>(dataRecords);

                    this.queryContext.BeginTrackingQuery();

                    foreach (var action in this.trackEntityActions)
                    {
                        action(this.queryContext.StateManager);
                    }

                    return result;
                }

                protected override object MapFromDynamicObjectGraph(object obj, Type targetType)
                {
                    object BaseImpl() => base.MapFromDynamicObjectGraph(obj, targetType);

                    // mapping required?
                    if (obj == null || targetType == obj.GetType())
                    {
                        return BaseImpl();
                    }

                    if (obj is DynamicObject dobj)
                    {
                        // is obj an entity?
                        if (this.TryMapEntity(dobj, out object entity))
                        {
                            return entity;
                        }

                        // is obj an array
                        if (this.TryMapArray(dobj, targetType, out object array))
                        {
                            return array;
                        }

                        // is obj a grouping
                        if (this.TryMapGrouping(dobj, targetType, out object grouping))
                        {
                            return grouping;
                        }
                    }

                    // is targetType a collection?
                    Type elementType = Utils.TryGetQueryResultSequenceType(targetType);
                    if (elementType == null)
                    {
                        return BaseImpl();
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

                private bool TryMapArray(DynamicObject dobj, Type targetType, out object array)
                {
                    array = null;

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
                        array = this.MapFromDynamicObjectGraph(elements, typeInfo.ResolveType(this.typeResolver));
                        return true;
                    }

                    return false;
                }

                private bool TryMapGrouping(DynamicObject dobj, Type targetType, out object grouping)
                {
                    grouping = null;

                    Type type = dobj.Type?.ResolveType(this.typeResolver) ?? targetType;

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

                private bool TryMapEntity(DynamicObject dobj, out object entity)
                {
                    entity = null;

                    if (!dobj.TryGet(@"__EntityType", out object entityTypeName))
                    {
                        return false;
                    }

                    if (!(entityTypeName is string))
                    {
                        return false;
                    }

                    if (!this.entityTypeMap.TryGetValue(entityTypeName.ToString(), out IEntityType entityType))
                    {
                        return false;
                    }

                    if (this.map.TryGetValue(dobj, out entity))
                    {
                        return true;
                    }

                    // Map only scalar properties for now, navigations must be set later
                    var valueBuffer = new ValueBuffer(
                        entityType
                            .GetProperties()
                            .Select(p => this.MapFromDynamicObjectGraph(dobj.Get(p.Name), p.ClrType))
                            .ToArray());

                    bool entityIsTracked = dobj.PropertyNames.Contains(@"__EntityLoadedCollections");

                    // Get entity instance from EFC's identity map, or create a new one
                    Func<MaterializationContext, object> materializer = this.entityMaterializerSource.GetMaterializer(entityType);
                    var materializationContext = new MaterializationContext(valueBuffer, this.queryContext.Context);

                    IKey pk = entityType.FindPrimaryKey();
                    if (pk != null)
                    {
                        entity = this.queryContext
                            .QueryBuffer
                            .GetEntity(
                                pk,
                                new EntityLoadInfo(
                                    materializationContext,
                                    materializer),
                                queryStateManager: entityIsTracked,
                                throwOnNullKey: false);
                    }

                    if (entity == null)
                    {
                        entity = materializer.Invoke(materializationContext);
                    }

                    this.map.Add(dobj, entity);
                    object entityNoRef = entity;

                    if (entityIsTracked)
                    {
                        var loadedCollections = this.Map<List<string>>(dobj.Get<DynamicObject>(@"__EntityLoadedCollections"));

                        this.trackEntityActions.Add(sm =>
                        {
                            InternalEntityEntry entry
                                = sm.StartTrackingFromQuery(entityType, entityNoRef, valueBuffer, handledForeignKeys: null);

                            foreach (INavigation nav in loadedCollections.Select(name => entry.EntityType.FindNavigation(name)))
                            {
                                entry.SetIsLoaded(nav);
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

                    public Expression Expression => throw new NotImplementedException();

                    public IQueryProvider Provider => throw new NotImplementedException();

                    public IEnumerator GetEnumerator() => throw new NotImplementedException();

                    internal static IQueryable<T> Create<T>()
                    {
                        return new RemoteQueryableStub<T>();
                    }
                }

                private class RemoteQueryableStub<T> : RemoteQueryableStub, IQueryable<T>
                {
                    public override Type ElementType => typeof(T);

                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotImplementedException();
                }
            }
        }
    }
}
