namespace InfoCarrier.Core.Server
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.Extensions.DependencyInjection;
    using Remote.Linq;
    using Remote.Linq.ExpressionVisitors;
    using MethodInfo = System.Reflection.MethodInfo;

    public sealed class QueryDataHelper : IDisposable
    {
        private static readonly MethodInfo ExecuteExpressionMethod
            = typeof(QueryDataHelper).GetTypeInfo().GetDeclaredMethod(nameof(ExecuteExpression));

        private static readonly MethodInfo ExecuteExpressionAsyncMethod
            = typeof(QueryDataHelper).GetTypeInfo().GetDeclaredMethod(nameof(ExecuteExpressionAsync));

        private static readonly MethodInfo DbContextSetMethod
            = Utils.GetMethodInfo<DbContext>(c => c.Set<object>()).GetGenericMethodDefinition();

        private readonly DbContext dbContext;
        private readonly System.Linq.Expressions.Expression linqExpression;
        private readonly ITypeResolver typeResolver = new TypeResolver();

        public QueryDataHelper(
            Func<DbContext> dbContextFactory,
            Remote.Linq.Expressions.Expression rlinq,
            QueryTrackingBehavior trackingBehavior)
        {
            this.dbContext = dbContextFactory();
            this.dbContext.ChangeTracker.QueryTrackingBehavior = trackingBehavior;

            // UGLY: this resembles Remote.Linq.Expressions.ExpressionExtensions.PrepareForExecution()
            // but excludes PartialEval (otherwise simple queries like db.Set<X>().First() are executed
            // prematurely)
            this.linqExpression = rlinq
                .ReplaceNonGenericQueryArgumentsByGenericArguments()
                .ReplaceResourceDescriptorsByQueryable(
                    this.typeResolver,
                    provider: type =>
                        DbContextSetMethod
                            .MakeGenericMethod(type)
                            .ToDelegate<Func<IQueryable>>(this.dbContext)
                            .Invoke())
                .ToLinqExpression(this.typeResolver);

            // Replace NullConditionalExpressionStub MethodCallExpression with NullConditionalExpression
            this.linqExpression = Utils.ReplaceNullConditional(this.linqExpression, false);
        }

        public IEnumerable<DynamicObject> QueryData()
        {
            var resultType = this.linqExpression.Type.GetGenericTypeImplementations(typeof(IQueryable<>)).Select(t => t.GetGenericArguments().Single()).FirstOrDefault();
            resultType = resultType == null ? this.linqExpression.Type : typeof(IEnumerable<>).MakeGenericType(resultType);

            object queryResult = ExecuteExpressionMethod
                .MakeGenericMethod(resultType)
                .ToDelegate<Func<object>>(this)
                .Invoke();

            if (queryResult is IEnumerable enumerable)
            {
                if (Utils.TryGetQueryResultSequenceType(resultType) != null)
                {
                    // TRICKY: sometimes EF returns enumerable result as ExceptionInterceptor<T> which
                    // isn't fully ready for mapping to DynamicObjects (some complex self-referencing navigation
                    // properties may not have received their values yet). We have to force materialization.
                    queryResult = enumerable.Cast<object>().ToList();
                }
                else
                {
                    // Little trick for a single result item of type IGrouping/array/string
                    queryResult = new[] { queryResult };
                }
            }

            return this.MapResult(queryResult);
        }

        public async Task<IEnumerable<DynamicObject>> QueryDataAsync()
        {
            Type elementType = Utils.TryGetQueryResultSequenceType(this.linqExpression.Type) ?? this.linqExpression.Type;

            object queryResult = await ExecuteExpressionAsyncMethod
                .MakeGenericMethod(elementType)
                .ToDelegate<Func<Task<object>>>(this)
                .Invoke();

            return this.MapResult(queryResult);
        }

        private object ExecuteExpression<T>()
        {
            IQueryProvider provider = this.dbContext.GetService<IAsyncQueryProvider>();
            return provider.Execute<T>(this.linqExpression);
        }

        private async Task<object> ExecuteExpressionAsync<T>()
        {
            IAsyncQueryProvider provider = this.dbContext.GetService<IAsyncQueryProvider>();

            var queryResult = new List<T>();
            using (var enumerator = provider.ExecuteAsync<T>(this.linqExpression).GetEnumerator())
            {
                while (await enumerator.MoveNext())
                {
                    queryResult.Add(enumerator.Current);
                }
            }

            return queryResult;
        }

        private IEnumerable<DynamicObject> MapResult(object queryResult)
        {
            IEnumerable<DynamicObject> result =
                Remote.Linq.Expressions.ExpressionExtensions.ConvertResultToDynamicObjects(
                    queryResult,
                    new EntityToDynamicObjectMapper(this.dbContext, this.typeResolver),
                    t => true);

            return result;
        }

        public void Dispose()
        {
            this.dbContext.Dispose();
        }

        private class EntityToDynamicObjectMapper : DynamicObjectMapper
        {
            private static readonly MethodInfo MapGroupingMethod
                = Utils.GetMethodInfo<EntityToDynamicObjectMapper>(x => x.MapGrouping<object, object>(null, null))
                    .GetGenericMethodDefinition();

            private readonly IStateManager stateManager;
            private readonly Dictionary<object, DynamicObject> cachedEntities =
                new Dictionary<object, DynamicObject>();

            public EntityToDynamicObjectMapper(DbContext dbContext, ITypeResolver typeResolver)
                : base(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true }, typeResolver)
            {
                this.stateManager = dbContext.GetInfrastructure<IServiceProvider>().GetRequiredService<IStateManager>();
            }

            protected override bool ShouldMapToDynamicObject(IEnumerable collection) =>
                collection.GetType().GetGenericTypeImplementations(typeof(IGrouping<,>)).Any()
                || base.ShouldMapToDynamicObject(collection);

            protected override DynamicObject MapToDynamicObjectGraph(object obj, Func<Type, bool> setTypeInformation)
            {
                if (obj == null)
                {
                    return null;
                }

                Type objType = obj.GetType();

                // Special mapping of arrays
                if (objType.IsArray)
                {
                    var array = ((IEnumerable)obj)
                        .Cast<object>()
                        .Select(x => this.MapToDynamicObjectGraph(x, setTypeInformation))
                        .ToArray();
                    var dto = new DynamicObject();
                    dto.Add("ArrayType", new Aqua.TypeSystem.TypeInfo(objType, includePropertyInfos: false));
                    dto.Add("Elements", array);
                    return dto;
                }

                // Special mapping of IGrouping<,>
                foreach (var groupingType in objType.GetGenericTypeImplementations(typeof(IGrouping<,>)))
                {
                    object mappedGrouping =
                        MapGroupingMethod
                            .MakeGenericMethod(groupingType.GenericTypeArguments)
                            .Invoke(this, new[] { obj, setTypeInformation });
                    return (DynamicObject)mappedGrouping;
                }

                // Special mapping of entities
                IEntityType entityType = this.stateManager.Context.Model.FindEntityType(objType);
                if (entityType != null)
                {
                    return this.MapToDynamicObjectGraph(obj, setTypeInformation, entityType);
                }

                return base.MapToDynamicObjectGraph(obj, setTypeInformation);
            }

            private DynamicObject MapToDynamicObjectGraph(object obj, Func<Type, bool> setTypeInformation, IEntityType entityType)
            {
                if (this.cachedEntities.TryGetValue(obj, out DynamicObject dto))
                {
                    return dto;
                }

                this.cachedEntities.Add(obj, dto = new DynamicObject(obj.GetType()));

                InternalEntityEntry entry = this.stateManager.GetOrCreateEntry(obj, entityType);
                dto.Add(@"__EntityType", entry.EntityType.DisplayName());

                if (entry.EntityState != EntityState.Detached)
                {
                    dto.Add(@"__EntityIsTracked", true);
                }

                dto.Add(
                    @"__EntityLoadedNavigations",
                    entry.EntityType.GetNavigations()
                        .Where(n => entry.IsLoaded(n))
                        .Select(n => n.Name).ToList());

                foreach (MemberEntry prop in entry.ToEntityEntry().Members)
                {
                    DynamicObject value = prop is ReferenceEntry refProp && refProp.TargetEntry != null
                        ? this.MapToDynamicObjectGraph(prop.CurrentValue, setTypeInformation, refProp.TargetEntry.Metadata)
                        : this.MapToDynamicObjectGraph(prop.CurrentValue, setTypeInformation);
                    dto.Add(prop.Metadata.Name, value);
                }

                return dto;
            }

            private DynamicObject MapGrouping<TKey, TElement>(IGrouping<TKey, TElement> grouping, Func<Type, bool> setTypeInformation)
            {
                var mappedGrouping = new DynamicObject(typeof(IGrouping<TKey, TElement>));
                mappedGrouping.Add("Key", this.MapToDynamicObjectGraph(grouping.Key, setTypeInformation));
                mappedGrouping.Add(
                    "Elements",
                    new DynamicObject(
                        this.MapCollection(grouping, setTypeInformation).ToList(),
                        this));
                return mappedGrouping;
            }
        }
    }
}
