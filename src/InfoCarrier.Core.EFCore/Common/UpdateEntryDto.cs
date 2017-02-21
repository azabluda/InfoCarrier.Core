namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.Update;

    [DataContract]
    public class UpdateEntryDto
    {
        public UpdateEntryDto()
        {
        }

        public UpdateEntryDto(IUpdateEntry entry)
        {
            this.EntityTypeName = entry.EntityType.Name;
            this.EntityState = entry.EntityState;
            this.PropertyDatas = entry.ToEntityEntry().Properties.Select(
                prop => new PropertyData
                {
                    OriginalValue = prop.OriginalValue,
                    CurrentValue = prop.CurrentValue,
                    IsModified = prop.IsModified,
                    IsTemporary = prop.IsTemporary,
                }).ToList();
        }

        [DataMember]
        public string EntityTypeName { get; private set; }

        [DataMember]
        public EntityState EntityState { get; private set; }

        [DataMember]
        private List<PropertyData> PropertyDatas { get; } = new List<PropertyData>();

        public IReadOnlyList<JoinedProperty> JoinScalarProperties(EntityEntry entry)
        {
            return entry.Properties.Zip(
                this.PropertyDatas,
                (ef, dto) => new JoinedProperty { EfProperty = ef, DtoProperty = dto }).ToList();
        }

        public struct JoinedProperty
        {
            public PropertyEntry EfProperty;
            public PropertyData DtoProperty;
        }

        [DataContract]
        public class PropertyData
        {
            // TODO: may this affect the performance?
            private static DynamicObjectMapper Mapper
                => new DynamicObjectMapper(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true }, new TypeResolver());

            [IgnoreDataMember]
            public object OriginalValue { get; set; }

            [IgnoreDataMember]
            public object CurrentValue { get; set; }

            [DataMember]
            private DynamicObject OriginalValueDto
            {
                get { return Mapper.MapObject(this.OriginalValue); }
                set { this.OriginalValue = Mapper.Map(value); }
            }

            [DataMember]
            private DynamicObject CurrentValueDto
            {
                get { return Mapper.MapObject(this.CurrentValue); }
                set { this.CurrentValue = Mapper.Map(value); }
            }

            [DataMember]
            public bool IsModified { get; set; }

            [DataMember]
            public bool IsTemporary { get; set; }
        }
    }
}
