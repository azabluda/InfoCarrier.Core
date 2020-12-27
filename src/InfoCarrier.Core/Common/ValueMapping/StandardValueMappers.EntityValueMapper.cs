// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common.ValueMapping
{
    using System.Collections.Generic;
    using System.Linq;
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.Metadata;

    /// <summary>
    /// Standard value mapper for EF entities.
    /// </summary>
    public static partial class StandardValueMappers
    {
        private class EntityValueMapper : IInfoCarrierValueMapper
        {
            private const string EntityType = @"__EntityType";
            private const string EntityLoadedNavigations = @"__EntityLoadedNavigations";

            public bool TryMapToDynamicObject(IMapToDynamicObjectContext context, out object mapped)
            {
                if (context.EntityEntry == null)
                {
                    mapped = null;
                    return false;
                }

                var dto = new DynamicObject(context.Object.GetType());
                context.AddToCache(dto);

                dto.Add(EntityType, context.EntityEntry.EntityType.DisplayName());

                if (context.EntityEntry.EntityState != EntityState.Detached)
                {
                    dto.Add(
                        EntityLoadedNavigations,
                        context.MapToDynamicObjectGraph(
                            context.EntityEntry.EntityType.GetNavigations()
                                .Where(n => context.EntityEntry.IsLoaded(n))
                                .Select(n => n.Name)
                                .ToList()));
                }

                foreach (MemberEntry prop in context.EntityEntry.ToEntityEntry().Members)
                {
                    DynamicObject value = context.MapToDynamicObjectGraph(
                        Utils.ConvertToProvider(prop.CurrentValue, prop.Metadata as IProperty));
                    dto.Add(prop.Metadata.Name, value);
                }

                mapped = dto;
                return true;
            }

            public bool TryMapFromDynamicObject(IMapFromDynamicObjectContext context, out object entity)
            {
                if (!context.Dto.TryGet(EntityType, out object entityTypeName))
                {
                    entity = null;
                    return false;
                }

                if (!(entityTypeName is string))
                {
                    entity = null;
                    return false;
                }

                if (context.Dto.TryGet(EntityLoadedNavigations, out object loadedNavigations))
                {
                    loadedNavigations = context.MapFromDynamicObjectGraph(loadedNavigations);
                }

                entity = context.TryMapEntity(
                    entityTypeName.ToString(),
                    loadedNavigations != null ? new HashSet<string>((IEnumerable<string>)loadedNavigations) : null);
                return entity != null;
            }
        }
    }
}
