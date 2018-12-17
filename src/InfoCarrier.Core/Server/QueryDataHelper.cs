// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Server
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.Common;
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

    /// <summary>
    ///     Server-side implementation of <see cref="IInfoCarrierBackend.QueryData" /> and
    ///     <see cref="IInfoCarrierBackend.QueryDataAsync" /> methods.
    /// </summary>
    public sealed class QueryDataHelper : IDisposable
    {
        private static readonly MethodInfo ExecuteExpressionMethod
            = typeof(QueryDataHelper).GetTypeInfo().GetDeclaredMethod(nameof(ExecuteExpression));

        private static readonly MethodInfo ExecuteExpressionAsyncMethod
            = typeof(QueryDataHelper).GetTypeInfo().GetDeclaredMethod(nameof(ExecuteExpressionAsync));

        private readonly DbContext dbContext;
        private readonly System.Linq.Expressions.Expression linqExpression;
        private readonly ITypeResolver typeResolver = new TypeResolver();
        private readonly ITypeInfoProvider typeInfoProvider = new TypeInfoProvider();

        /// <summary>
        ///     Initializes a new instance of the <see cref="QueryDataHelper" /> class.
        /// </summary>
        /// <param name="dbContextFactory"> Factory for <see cref="DbContext" /> against which the requested query will be executed. </param>
        /// <param name="request"> The <see cref="QueryDataRequest" /> object from the client containing the query. </param>
        public QueryDataHelper(
            Func<DbContext> dbContextFactory,
            QueryDataRequest request)
        {
            this.dbContext = dbContextFactory();
            this.dbContext.ChangeTracker.QueryTrackingBehavior = request.TrackingBehavior;
            IAsyncQueryProvider provider = this.dbContext.GetService<IAsyncQueryProvider>();

            // UGLY: this resembles Remote.Linq.Expressions.ExpressionExtensions.PrepareForExecution()
            // but excludes PartialEval (otherwise simple queries like db.Set<X>().First() are executed
            // prematurely)
            this.linqExpression = request.Query
                .ReplaceNonGenericQueryArgumentsByGenericArguments()
                .ReplaceResourceDescriptorsByQueryable(
                    this.typeResolver,
                    provider: type => (IQueryable)Activator.CreateInstance(typeof(EntityQueryable<>).MakeGenericType(type), provider))
                .ToLinqExpression(this.typeResolver);

            // Replace NullConditionalExpressionStub MethodCallExpression with NullConditionalExpression
            this.linqExpression = Utils.ReplaceNullConditional(this.linqExpression, false);
        }

        /// <summary>
        ///     Executes the requested query against the actual database.
        /// </summary>
        /// <returns>
        ///     The result of the query execution.
        /// </returns>
        public QueryDataResult QueryData()
        {
            bool queryReturnsSingleResult = Utils.QueryReturnsSingleResult(this.linqExpression);
            Type resultType = queryReturnsSingleResult
                ? this.linqExpression.Type
                : typeof(IEnumerable<>).MakeGenericType(this.linqExpression.Type.GenericTypeArguments.First());

            object queryResult = ExecuteExpressionMethod
                .MakeGenericMethod(resultType)
                .ToDelegate<Func<object>>(this)
                .Invoke();

            if (queryReturnsSingleResult)
            {
                // Little trick for a single result item of type
                queryResult = new[] { queryResult };
            }
            else
            {
                // TRICKY: sometimes EF returns enumerable result as ExceptionInterceptor<T> which
                // isn't fully ready for mapping to DynamicObjects (some complex self-referencing navigation
                // properties may not have received their values yet). We have to force materialization.
                queryResult = ((IEnumerable)queryResult).Cast<object>().ToList();
            }

            return new QueryDataResult(this.MapResult(queryResult));
        }

        /// <summary>
        ///     Asynchronously executes the requested query against the actual database.
        /// </summary>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> to observe while waiting for the task to
        ///     complete.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous operation.
        ///     The task result contains the result of the query execution.
        /// </returns>
        public async Task<QueryDataResult> QueryDataAsync(CancellationToken cancellationToken = default)
        {
            Type resultType = Utils.QueryReturnsSingleResult(this.linqExpression)
                ? this.linqExpression.Type
                : Utils.TryGetSequenceType(this.linqExpression.Type);

            object queryResult = await ExecuteExpressionAsyncMethod
                .MakeGenericMethod(resultType)
                .ToDelegate<Func<CancellationToken, Task<object>>>(this)
                .Invoke(cancellationToken);

            return new QueryDataResult(this.MapResult(queryResult));
        }

        private object ExecuteExpression<T>()
        {
            IQueryProvider provider = this.dbContext.GetService<IAsyncQueryProvider>();
            return provider.Execute<T>(this.linqExpression);
        }

        private async Task<object> ExecuteExpressionAsync<T>(CancellationToken cancellationToken)
        {
            IAsyncQueryProvider provider = this.dbContext.GetService<IAsyncQueryProvider>();

            var queryResult = new List<T>();
            using (var enumerator = provider.ExecuteAsync<T>(this.linqExpression).GetEnumerator())
            {
                while (await enumerator.MoveNext(cancellationToken))
                {
                    queryResult.Add(enumerator.Current);
                }
            }

            return queryResult;
        }

        private IEnumerable<DynamicObject> MapResult(object queryResult)
            => new EntityToDynamicObjectMapper(this.dbContext, this.typeResolver, this.typeInfoProvider).MapCollection(queryResult, t => true);

        /// <summary>
        ///     Disposes the <see cref="DbContext" /> against which the requested query has been executed.
        /// </summary>
        public void Dispose()
        {
            this.dbContext.Dispose();
        }

        private class EntityToDynamicObjectMapper : DynamicObjectMapper
        {
            private static readonly MethodInfo MapGroupingMethod
                = Utils.GetMethodInfo<EntityToDynamicObjectMapper>(x => x.MapGrouping<object, object>(null, null))
                    .GetGenericMethodDefinition();

            private static readonly MethodInfo MapCollectionMethod
                = Utils.GetMethodInfo<EntityToDynamicObjectMapper>(x => x.MapEnumerable<object>(null, null))
                    .GetGenericMethodDefinition();

            private readonly IStateManager stateManager;
            private readonly IInternalEntityEntryFactory entityEntryFactory;
            private readonly IReadOnlyDictionary<Type, IEntityType> detachedEntityTypeMap;
            private readonly Dictionary<object, DynamicObject> cachedDtos =
                new Dictionary<object, DynamicObject>();

            public EntityToDynamicObjectMapper(DbContext dbContext, ITypeResolver typeResolver, ITypeInfoProvider typeInfoProvider)
                : base(typeResolver, typeInfoProvider, new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true })
            {
                IServiceProvider serviceProvider = dbContext.GetInfrastructure();
                this.stateManager = serviceProvider.GetRequiredService<IStateManager>();
                this.entityEntryFactory = serviceProvider.GetRequiredService<IInternalEntityEntryFactory>();
                this.detachedEntityTypeMap = dbContext.Model.GetEntityTypes()
                    .Where(et => et.ClrType != null)
                    .GroupBy(et => et.ClrType)
                    .ToDictionary(x => x.Key, x => x.First());
            }

            protected override bool ShouldMapToDynamicObject(IEnumerable collection) =>
                !(collection is List<DynamicObject>);

            protected override DynamicObject MapToDynamicObjectGraph(object obj, Func<Type, bool> setTypeInformation)
            {
                if (obj == null)
                {
                    return null;
                }

                if (this.cachedDtos.TryGetValue(obj, out DynamicObject cached))
                {
                    return cached;
                }

                Type objType = obj.GetType();

                // Special mapping of arrays
                if (objType.IsArray)
                {
                    DynamicObject dto = this.CreateAndCacheDynamicObject(obj, null);

                    var array = ((IEnumerable)obj)
                        .Cast<object>()
                        .Select(x => this.MapToDynamicObjectGraph(x, setTypeInformation))
                        .ToArray();
                    dto.Add(@"ArrayType", new Aqua.TypeSystem.TypeInfo(objType, includePropertyInfos: false));
                    dto.Add(@"Elements", array);
                    return dto;
                }

                // Special mapping of IGrouping<,>
                foreach (var groupingType in Utils.GetGenericTypeImplementations(objType, typeof(IGrouping<,>)))
                {
                    object mappedGrouping =
                        MapGroupingMethod
                            .MakeGenericMethod(groupingType.GenericTypeArguments)
                            .Invoke(this, new[] { obj, setTypeInformation });
                    return (DynamicObject)mappedGrouping;
                }

                // Special mapping of collections
                if (objType != typeof(string))
                {
                    Type elementType = Utils.TryGetSequenceType(objType);
                    if (elementType != null && elementType != typeof(DynamicObject))
                    {
                        object mappedEnumerable = MapCollectionMethod
                            .MakeGenericMethod(elementType)
                            .Invoke(this, new[] { obj, setTypeInformation });
                        return (DynamicObject)mappedEnumerable;
                    }
                }

                // Check if obj is a tracked or detached entity
                InternalEntityEntry entry = this.stateManager.TryGetEntry(obj);
                if (entry == null
                    && this.detachedEntityTypeMap.TryGetValue(objType, out IEntityType entityType))
                {
                    // Create detached entity entry
                    entry = this.stateManager.GetOrCreateEntry(obj, entityType);
                }

                return entry == null
                    ? base.MapToDynamicObjectGraph(obj, setTypeInformation) // Default mapping
                    : this.MapToDynamicObjectGraph(obj, setTypeInformation, entry); // Special mapping of entities
            }

            private DynamicObject MapToDynamicObjectGraph(object obj, Func<Type, bool> setTypeInformation, InternalEntityEntry entry)
            {
                if (this.cachedDtos.TryGetValue(obj, out DynamicObject cached))
                {
                    return cached;
                }

                DynamicObject dto = this.CreateAndCacheDynamicObject(obj, obj.GetType());

                if (entry.Entity.GetType() != entry.EntityType.ClrType)
                {
                    IEntityType entityType = this.stateManager.Context.Model.FindEntityType(entry.Entity.GetType());
                    if (entityType != null)
                    {
                        entry = this.entityEntryFactory.Create(this.stateManager, entityType, entry.Entity);
                    }
                }

                dto.Add(@"__EntityType", entry.EntityType.DisplayName());

                if (entry.EntityState != EntityState.Detached)
                {
                    dto.Add(
                        @"__EntityLoadedNavigations",
                        this.MapToDynamicObjectGraph(
                            entry.EntityType.GetNavigations()
                                .Where(n => entry.IsLoaded(n))
                                .Select(n => n.Name).ToList(),
                            setTypeInformation));
                }

                foreach (MemberEntry prop in entry.ToEntityEntry().Members)
                {
                    DynamicObject value = prop is ReferenceEntry refProp && refProp.TargetEntry != null
                        ? this.MapToDynamicObjectGraph(prop.CurrentValue, setTypeInformation, refProp.TargetEntry.GetInfrastructure())
                        : this.MapToDynamicObjectGraph(
                            Utils.ConvertToProvider(prop.CurrentValue, prop.Metadata as IProperty),
                            setTypeInformation);
                    dto.Add(prop.Metadata.Name, value);
                }

                return dto;
            }

            private DynamicObject MapEnumerable<TElement>(IEnumerable<TElement> enumerable, Func<Type, bool> setTypeInformation)
            {
                DynamicObject dto = this.CreateAndCacheDynamicObject(enumerable, typeof(IEnumerable<TElement>));

                dto.Add(
                    @"Elements",
                    new DynamicObject(
                        this.MapCollection(enumerable.ToList(), setTypeInformation).ToList(),
                        this));

                var collectionType = enumerable.GetType();
                if (collectionType != typeof(List<TElement>))
                {
                    bool hasDefaultCtor = collectionType.GetTypeInfo().DeclaredConstructors.Any(
                        c => !c.IsStatic && c.IsPublic && c.GetParameters().Length == 0);
                    if (hasDefaultCtor)
                    {
                        dto.Add(@"CollectionType", new Aqua.TypeSystem.TypeInfo(collectionType, includePropertyInfos: false));
                    }
                }

                return dto;
            }

            private DynamicObject MapGrouping<TKey, TElement>(IGrouping<TKey, TElement> grouping, Func<Type, bool> setTypeInformation)
            {
                DynamicObject dto = this.CreateAndCacheDynamicObject(grouping, typeof(IGrouping<TKey, TElement>));

                dto.Add(@"Key", this.MapToDynamicObjectGraph(grouping.Key, setTypeInformation));
                dto.Add(
                    @"Elements",
                    new DynamicObject(
                        this.MapCollection(grouping, setTypeInformation).ToList(),
                        this));
                return dto;
            }

            private DynamicObject CreateAndCacheDynamicObject(object obj, Type type)
            {
                var dto = new DynamicObject(type);
                this.cachedDtos.Add(obj, dto);
                return dto;
            }
        }
    }
}
