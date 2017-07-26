namespace InfoCarrier.Core.Common
{
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
        public UpdateEntryDto()
        {
        }

        public UpdateEntryDto(IUpdateEntry entry)
        {
            this.EntityTypeName = entry.EntityType.DisplayName();
            this.EntityState = entry.EntityState;
            this.PropertyDatas = entry.ToEntityEntry().Properties.Select(
                prop => new PropertyData
                {
                    Name = prop.Metadata.Name,
                    OriginalValue = prop.Metadata.GetOriginalValueIndex() >= 0 ? prop.OriginalValue : null,
                    CurrentValue = prop.CurrentValue,
                    IsModified = prop.IsModified,
                    IsTemporary = prop.IsTemporary,
                }).ToList();

            if (entry.EntityType.HasDelegatedIdentity())
            {
                var ownership = entry.EntityType.GetForeignKeys().Single(fk => fk.IsOwnership);
                this.DelegatedIdentityDatas = ownership.Properties.Select(
                    prop => new PropertyData
                    {
                        Name = prop.Name,
                        OriginalValue = entry.GetOriginalValue(prop),
                        CurrentValue = entry.GetCurrentValue(prop),
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
        private List<PropertyData> PropertyDatas { get; } = new List<PropertyData>();

        [DataMember]
        private List<PropertyData> DelegatedIdentityDatas { get; } = new List<PropertyData>();

        public IReadOnlyList<JoinedProperty> JoinScalarProperties(EntityEntry entry)
        {
            return (
                from ef in entry.Properties
                join dto in this.PropertyDatas
                on ef.Metadata.Name equals dto.Name
                select new JoinedProperty { EfProperty = ef, DtoProperty = dto }).ToList();
        }

        public object[] GetCurrentValues(IEntityType entityType)
        {
            return entityType
                .GetProperties()
                .Select(p => this.PropertyDatas.SingleOrDefault(pd => pd.Name == p.Name)?.CurrentValue)
                .ToArray();
        }

        public object[] GetDelegatedIdentityKeys()
        {
            return this.DelegatedIdentityDatas.Select(d => d.CurrentValue).ToArray();
        }

        public struct JoinedProperty
        {
            public PropertyEntry EfProperty;
            public PropertyData DtoProperty;
        }

        [DataContract]
        public class PropertyData
        {
            private static readonly DynamicObjectMapper Mapper
                = new DynamicObjectMapper(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true });

            [DataMember]
            public string Name { get; set; }

            [IgnoreDataMember]
            public object OriginalValue { get; set; }

            [IgnoreDataMember]
            public object CurrentValue { get; set; }

            [DataMember]
            private DynamicObject OriginalValueDto
            {
                get => Mapper.MapObject(this.OriginalValue);
                set => this.OriginalValue = Mapper.Map(value);
            }

            [DataMember]
            private DynamicObject CurrentValueDto
            {
                get => Mapper.MapObject(this.CurrentValue);
                set => this.CurrentValue = Mapper.Map(value);
            }

            [DataMember]
            public bool IsModified { get; set; }

            [DataMember]
            public bool IsTemporary { get; set; }
        }
    }
}
