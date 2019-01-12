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
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Common.ValueMapping;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.Extensions.DependencyInjection;
    using Remote.Linq;
    using Remote.Linq.ExpressionVisitors;
    using MethodInfo = System.Reflection.MethodInfo;

    /// <summary>
    ///     Implementation of <see cref="IInfoCarrierServer.QueryData" /> and
    ///     <see cref="IInfoCarrierServer.QueryDataAsync" /> methods.
    /// </summary>
    internal class QueryDataHelper
    {
        private static readonly MethodInfo ExecuteExpressionMethod
            = typeof(QueryDataHelper).GetTypeInfo().GetDeclaredMethod(nameof(ExecuteExpression));

        private static readonly MethodInfo ExecuteExpressionAsyncMethod
            = typeof(QueryDataHelper).GetTypeInfo().GetDeclaredMethod(nameof(ExecuteExpressionAsync));

        private readonly DbContext dbContext;
        private readonly IEnumerable<IInfoCarrierValueMapper> valueMappers;
        private readonly System.Linq.Expressions.Expression linqExpression;
        private readonly ITypeResolver typeResolver = new TypeResolver();
        private readonly ITypeInfoProvider typeInfoProvider = new TypeInfoProvider();

        /// <summary>
        ///     Initializes a new instance of the <see cref="QueryDataHelper" /> class.
        /// </summary>
        /// <param name="dbContext"> <see cref="DbContext" /> against which the requested query will be executed. </param>
        /// <param name="request"> The <see cref="QueryDataRequest" /> object from the client containing the query. </param>
        /// <param name="customValueMappers"> Custom value mappers. </param>
        public QueryDataHelper(
            DbContext dbContext,
            QueryDataRequest request,
            IEnumerable<IInfoCarrierValueMapper> customValueMappers)
        {
            this.dbContext = dbContext;
            this.valueMappers = customValueMappers.Concat(StandardValueMappers.Mappers);

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
            => new EntityToDynamicObjectMapper(this.dbContext, this.typeResolver, this.typeInfoProvider, this.valueMappers)
                .MapCollection(queryResult, t => true);

        private class EntityToDynamicObjectMapper : DynamicObjectMapper
        {
            private readonly IEnumerable<IInfoCarrierValueMapper> valueMappers;
            private readonly IStateManager stateManager;
            private readonly IReadOnlyDictionary<Type, IEntityType> detachedEntityTypeMap;
            private readonly Dictionary<object, DynamicObject> cachedDtos =
                new Dictionary<object, DynamicObject>();

            public EntityToDynamicObjectMapper(
                DbContext dbContext,
                ITypeResolver typeResolver,
                ITypeInfoProvider typeInfoProvider,
                IEnumerable<IInfoCarrierValueMapper> valueMappers)
                : base(typeResolver, typeInfoProvider, new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true })
            {
                this.valueMappers = valueMappers;
                IServiceProvider serviceProvider = dbContext.GetInfrastructure();
                this.stateManager = serviceProvider.GetRequiredService<IStateManager>();
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

                InternalEntityEntry EntityEntryGetter()
                {
                    // Check if obj is a tracked or detached entity
                    InternalEntityEntry entry = this.stateManager.TryGetEntry(obj);
                    if (entry == null && this.detachedEntityTypeMap.TryGetValue(obj.GetType(), out IEntityType entityType))
                    {
                        // Create detached entity entry
                        entry = this.stateManager.GetOrCreateEntry(obj, entityType);
                    }

                    return entry;
                }

                var valueMappingContext = new MapToDynamicObjectContext(obj, EntityEntryGetter, this, setTypeInformation);
                foreach (IInfoCarrierValueMapper valueMapper in this.valueMappers)
                {
                    if (valueMapper.TryMapToDynamicObject(valueMappingContext, out DynamicObject dto))
                    {
                        return dto;
                    }
                }

                return base.MapToDynamicObjectGraph(obj, setTypeInformation);
            }

            private class MapToDynamicObjectContext : IMapToDynamicObjectContext
            {
                private readonly EntityToDynamicObjectMapper mapper;
                private readonly Func<Type, bool> setTypeInformation;
                private readonly Lazy<InternalEntityEntry> entityEntry;

                public MapToDynamicObjectContext(
                    object obj,
                    Func<InternalEntityEntry> entityEntryGetter,
                    EntityToDynamicObjectMapper mapper,
                    Func<Type, bool> setTypeInformation)
                {
                    this.mapper = mapper;
                    this.setTypeInformation = setTypeInformation;
                    this.Object = obj;
                    this.entityEntry = new Lazy<InternalEntityEntry>(entityEntryGetter);
                }

                public object Object { get; }

                public InternalEntityEntry EntityEntry => entityEntry.Value;

                public DynamicObject MapToDynamicObjectGraph(object obj)
                    => this.mapper.MapToDynamicObjectGraph(obj, this.setTypeInformation);

                public void AddToCache(DynamicObject dynamicObject)
                    => this.mapper.cachedDtos.Add(this.Object, dynamicObject);
            }
        }
    }
}
