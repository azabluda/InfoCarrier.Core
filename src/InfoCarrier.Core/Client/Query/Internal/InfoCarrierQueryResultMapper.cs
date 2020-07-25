// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Common.ValueMapping;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Storage;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InfoCarrierQueryResultMapper : DynamicObjectMapper
    {
        private readonly QueryContext queryContext;
        private readonly ITypeResolver typeResolver;
        private readonly IReadOnlyDictionary<string, IEntityType> entityTypeMap;
        private readonly IEntityMaterializerSource entityMaterializerSource;
        private readonly IEnumerable<IInfoCarrierValueMapper> valueMappers;
        private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();
        private readonly List<Action<IStateManager>> trackEntityActions = new List<Action<IStateManager>>();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1642:ConstructorSummaryDocumentationMustBeginWithStandardText", Justification = "InfoCarrier.Core internal.")]
        public InfoCarrierQueryResultMapper(
            QueryContext queryContext,
            ITypeResolver typeResolver,
            ITypeInfoProvider typeInfoProvider,
            IReadOnlyDictionary<string, IEntityType> entityTypeMap = null)
            : base(typeResolver, typeInfoProvider, new DynamicObjectMapperSettings { FormatNativeTypesAsString = true })
        {
            this.queryContext = queryContext;
            this.typeResolver = typeResolver;
            this.entityTypeMap = entityTypeMap ?? BuildEntityTypeMap(queryContext.Context);
            this.entityMaterializerSource = queryContext.Context.GetService<IEntityMaterializerSource>();
            this.valueMappers = queryContext.Context.GetService<IEnumerable<IInfoCarrierValueMapper>>()
                .Concat(StandardValueMappers.Mappers);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        internal static IReadOnlyDictionary<string, IEntityType> BuildEntityTypeMap(DbContext context)
            => context.Model.GetEntityTypes().ToDictionary(x => x.DisplayName());

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1618:GenericTypeParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        public IEnumerable<TResult> MapAndTrackResults<TResult>(IEnumerable<DynamicObject> dataRecords)
        {
            var result = this.Map<TResult>(dataRecords);

            //this.queryContext.BeginTrackingQuery();

            foreach (var action in this.trackEntityActions)
            {
                action(this.queryContext.StateManager);
            }

            return result;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "InfoCarrier.Core internal.")]
        protected override object MapFromDynamicObjectGraph(object obj, Type targetType)
        {
            // mapping required?
            if (obj == null || targetType == obj.GetType())
            {
                return base.MapFromDynamicObjectGraph(obj, targetType);
            }

            if (obj is DynamicObject dobj)
            {
                if (this.map.TryGetValue(dobj, out object cached))
                {
                    return cached;
                }

                var valueMappingContext = new MapFromDynamicObjectContext(dobj, this);
                foreach (IInfoCarrierValueMapper valueMapper in this.valueMappers)
                {
                    if (!valueMapper.TryMapFromDynamicObject(valueMappingContext, out var mapped))
                    {
                        continue;
                    }

                    if (mapped is DynamicObject)
                    {
                        obj = mapped;
                        break;
                    }

                    return mapped;
                }
            }

            return base.MapFromDynamicObjectGraph(obj, targetType);
        }

        private object TryMapEntity(IMapFromDynamicObjectContext context, string entityTypeName, IReadOnlyList<string> loadedNavigations)
        {
            if (!this.entityTypeMap.TryGetValue(entityTypeName, out IEntityType entityType))
            {
                return null;
            }

            // Map only scalar properties for now, navigations have to be set later
            var valueBuffer = new ValueBuffer(
                entityType
                    .GetProperties()
                    .Select(p =>
                    {
                        object value = context.Dto.Get(p.Name);
                        if (p.GetValueConverter() != null)
                        {
                            value = context.MapFromDynamicObjectGraph(value);
                            value = Utils.ConvertFromProvider(value, p);
                        }

                        return context.MapFromDynamicObjectGraph(value, p.ClrType);
                    })
                    .ToArray());

            bool entityIsTracked = loadedNavigations != null;

            // Get entity instance from EFC's identity map, or create a new one
            object entity = null;
            IKey pk = entityType.FindPrimaryKey();
            if (pk != null && entityIsTracked)
            {
                entity = this.queryContext
                    .StateManager
                    .TryGetEntry(pk, pk.Properties.Select(p => valueBuffer[p.GetIndex()]).ToArray())
                    ?.Entity;
            }

            if (entity == null)
            {
                Func<MaterializationContext, object> materializer = this.entityMaterializerSource.GetMaterializer(entityType);
                var materializationContext = new MaterializationContext(valueBuffer, this.queryContext.Context);
                entity = materializer.Invoke(materializationContext);
            }

            context.AddToCache(entity);
            object entityNoRef = entity;

            if (entityIsTracked)
            {
                this.trackEntityActions.Add(sm =>
                {
                    InternalEntityEntry entry
                        = sm.StartTrackingFromQuery(entityType, entityNoRef, default);

                    foreach (INavigation nav in loadedNavigations.Select(name => entry.EntityType.FindNavigation(name)))
                    {
                        entry.SetIsLoaded(nav);
                    }
                });
            }

            // Set navigation properties AFTER adding to map to avoid endless recursion
            foreach (INavigation navigation in entityType.GetNavigations())
            {
                // TODO: shall we skip already loaded navigations if the entity is already tracked?
                if (context.Dto.TryGet(navigation.Name, out object value) && value != null)
                {
                    value = context.MapFromDynamicObjectGraph(value, navigation.ClrType);
                    if (navigation.IsCollection())
                    {
                        // TODO: clear or skip collection if it already contains something?
                        var coll = navigation.GetCollectionAccessor();
                        foreach (var item in (IEnumerable)value)
                        {
                            coll.Add(entity, item, false);
                        }
                    }
                    else
                    {
                        var mem = navigation.GetMemberInfo(false, true);
                        if (mem is System.Reflection.FieldInfo fieldInfo)
                        {
                            fieldInfo.SetValue(entity, value);
                        }
                        else if (mem is System.Reflection.PropertyInfo propInfo)
                        {
                            propInfo.SetValue(entity, value);
                        }
                    }
                }
            }

            return entity;
        }

        private class MapFromDynamicObjectContext : IMapFromDynamicObjectContext
        {
            private readonly InfoCarrierQueryResultMapper mapper;

            public MapFromDynamicObjectContext(DynamicObject dto, InfoCarrierQueryResultMapper mapper)
            {
                this.mapper = mapper;
                this.Dto = dto;
            }

            public DynamicObject Dto { get; }

            public ITypeResolver TypeResolver => this.mapper.typeResolver;

            public object MapFromDynamicObjectGraph(object obj, Type targetType = null)
                => this.mapper.MapFromDynamicObjectGraph(obj, targetType ?? typeof(object));

            public void AddToCache(object obj)
                => this.mapper.map.Add(this.Dto, obj);

            public object TryMapEntity(
                string entityTypeName,
                IReadOnlyList<string> loadedNavigations)
                => this.mapper.TryMapEntity(this, entityTypeName, loadedNavigations);
        }
    }
}
