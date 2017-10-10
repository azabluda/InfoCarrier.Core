// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Update;

    [DataContract]
    public class UpdateEntryDto
    {
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public UpdateEntryDto()
        {
        }

        public UpdateEntryDto(IUpdateEntry entry, IDynamicObjectMapper mapper)
        {
            this.EntityTypeName = entry.EntityType.DisplayName();
            this.EntityState = entry.EntityState;
            this.PropertyDatas = entry.ToEntityEntry().Properties.Select(
                prop => new PropertyData
                {
                    Name = prop.Metadata.Name,
                    OriginalValueDto = prop.Metadata.GetOriginalValueIndex() >= 0 ? mapper.MapObject(prop.OriginalValue) : null,
                    CurrentValueDto = mapper.MapObject(prop.CurrentValue),
                    IsModified = prop.IsModified,
                    IsTemporary = prop.IsTemporary,
                }).ToList();

            if (entry.EntityType.HasDefiningNavigation())
            {
                var ownership = entry.EntityType.GetForeignKeys().Single(fk => fk.IsOwnership);
                this.DelegatedIdentityDatas = ownership.Properties.Select(
                    prop => new PropertyData
                    {
                        Name = prop.Name,
                        OriginalValueDto = mapper.MapObject(entry.GetOriginalValue(prop)),
                        CurrentValueDto = mapper.MapObject(entry.GetCurrentValue(prop)),
                        IsModified = entry.IsModified(prop),
                        IsTemporary = entry.HasTemporaryValue(prop),
                    }).ToList();
            }
        }

        [DataMember]
        public string EntityTypeName { get; private set; }

        [DataMember]
        public EntityState EntityState { get; private set; }

        [DataMember]
        private List<PropertyData> PropertyDatas { get; set; } = new List<PropertyData>();

        [DataMember]
        private List<PropertyData> DelegatedIdentityDatas { get; set; } = new List<PropertyData>();

        public IReadOnlyList<(PropertyEntry EfProperty, PropertyData DtoProperty, object OriginalValue, object CurrentValue)> JoinScalarProperties(
            EntityEntry entry,
            IDynamicObjectMapper mapper)
        {
            return (
                from ef in entry.Properties
                join dto in this.PropertyDatas
                on ef.Metadata.Name equals dto.Name
                select (ef, dto, mapper.Map(dto.OriginalValueDto), mapper.Map(dto.CurrentValueDto))).ToList();
        }

        public object[] GetCurrentValues(IEntityType entityType, IDynamicObjectMapper mapper)
        {
            return entityType
                .GetProperties()
                .Select(p => mapper.Map(this.PropertyDatas.SingleOrDefault(pd => pd.Name == p.Name)?.CurrentValueDto))
                .ToArray();
        }

        public object[] GetDelegatedIdentityKeys(IDynamicObjectMapper mapper)
        {
            return this.DelegatedIdentityDatas.Select(d => mapper.Map(d.CurrentValueDto)).ToArray();
        }

        [DataContract]
        public class PropertyData
        {
            [DataMember]
            public string Name { get; set; }

            [DataMember]
            internal DynamicObject OriginalValueDto { get; set; }

            [DataMember]
            internal DynamicObject CurrentValueDto { get; set; }

            [DataMember]
            public bool IsModified { get; set; }

            [DataMember]
            public bool IsTemporary { get; set; }
        }
    }
}
