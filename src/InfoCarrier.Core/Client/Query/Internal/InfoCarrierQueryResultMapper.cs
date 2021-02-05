// Copyright (c) Alexander Zabluda. All rights reserved.
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
    using Microsoft.EntityFrameworkCore.Query;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InfoCarrierQueryResultMapper : DynamicObjectMapper
    {
        private readonly QueryContext queryContext;
        private readonly ITypeResolver typeResolver;
        private readonly IReadOnlyDictionary<string, IEntityType> entityTypeMap;
        private readonly IStateManager stateManager;
        private readonly IEnumerable<IInfoCarrierValueMapper> valueMappers;
        private readonly Dictionary<DynamicObject, object> map = new Dictionary<DynamicObject, object>();

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
            this.stateManager = queryContext.Context.GetService<IStateManager>();
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

        private object TryMapEntity(IMapFromDynamicObjectContext context, string entityTypeName, ISet<string> loadedNavigations)
        {
            if (!this.entityTypeMap.TryGetValue(entityTypeName, out IEntityType entityType))
            {
                return null;
            }

            // Map only scalar properties for now, navigations are to be set later
            var values = entityType
                .GetProperties()
                .ToDictionary(
                    p => p.Name,
                    p =>
                    {
                        object value = context.Dto.Get(p.Name);
                        if (p.GetValueConverter() != null)
                        {
                            value = context.MapFromDynamicObjectGraph(value);
                            value = Utils.ConvertFromProvider(value, p);
                        }

                        return context.MapFromDynamicObjectGraph(value, p.ClrType);
                    });

            bool entityIsTracked = loadedNavigations != null;

            // Get entity instance from EFC's identity map, or create a new one
            InternalEntityEntry entry = null;
            IKey pk = entityType.FindPrimaryKey();
            if (pk != null && entityIsTracked)
            {
                entry = this.stateManager.TryGetEntry(pk, pk.Properties.Select(p => values[p.Name]).ToArray());
            }

            if (entry == null)
            {
                entry = this.stateManager.CreateEntry(values, entityType);
            }

            var entity = entry.Entity;
            context.AddToCache(entity);

            // Set navigation properties AFTER adding to map to avoid endless recursion
            foreach (INavigationBase navigation in Utils.GetAllNavigations(entityType))
            {
                // Avoid accidental loading of navigations of a tracked entity
                if (entry.EntityState != EntityState.Detached &&
                    !entry.IsLoaded(navigation) &&
                    !loadedNavigations.Contains(navigation.Name))
                {
                    continue;
                }

                // TODO: shall we skip already loaded navigations if the entity is already tracked?
                if (context.Dto.TryGet(navigation.Name, out object value) && value != null)
                {
                    value = context.MapFromDynamicObjectGraph(value, navigation.ClrType);
                    if (navigation.IsCollection)
                    {
                        // TODO: clear or skip collection if it already contains something?
                        var coll = navigation.GetCollectionAccessor();
                        coll.GetOrCreate(entity, forMaterialization: true);
                        foreach (var item in (IEnumerable)value)
                        {
                            coll.Add(entity, item, forMaterialization: true);
                        }
                    }
                    else
                    {
                        var mem = navigation.GetMemberInfo(forMaterialization: true, forSet: true);
                        if (mem is System.Reflection.FieldInfo fieldInfo)
                        {
                            fieldInfo.SetValue(entity, value);
                        }
                        else if (mem is System.Reflection.PropertyInfo propInfo)
                        {
                            propInfo.SetValue(entity, value);
                        }
                    }

                    SetIsLoadedWhenNoTracking(navigation, entity);
                }
            }

            if (entityIsTracked)
            {
                if (entry.EntityState == EntityState.Detached)
                {
                    entry.SetEntityState(EntityState.Unchanged);
                }

                foreach (INavigationBase navigation in Utils.GetAllNavigations(entityType).Where(n => loadedNavigations.Contains(n.Name)))
                {
                    entry.SetIsLoaded(navigation);
                }
            }

            return entity;
        }

        private static void SetIsLoadedWhenNoTracking(INavigationBase navigation, object entity)
        {
            var serviceProperties = navigation
                .DeclaringEntityType
                .GetDerivedTypesInclusive()
                .Where(t => t.ClrType.IsInstanceOfType(entity))
                .SelectMany(e => e.GetServiceProperties())
                .Where(p => p.ClrType == typeof(ILazyLoader));

            foreach (var serviceProperty in serviceProperties)
            {
                ((ILazyLoader)serviceProperty.GetGetter().GetClrValue(entity))?.SetLoaded(entity, navigation.Name);
            }
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
                ISet<string> loadedNavigations)
                => this.mapper.TryMapEntity(this, entityTypeName, loadedNavigations);
        }
    }
}
