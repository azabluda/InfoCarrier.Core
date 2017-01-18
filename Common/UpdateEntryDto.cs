namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
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
            this.PropertyDatas = entry.EntityType.GetProperties().ToDictionary(
                prop => prop.Name,
                prop => new PropertyData
                {
                    OriginalValue = entry.GetOriginalValue(prop),
                    CurrentValue = entry.GetCurrentValue(prop),
                    IsModified = entry.IsModified(prop),
                    HasTemporaryValue = entry.HasTemporaryValue(prop),
                    IsStoreGenerated = entry.IsStoreGenerated(prop),
                });
        }

        [DataMember]
        public string EntityTypeName { get; private set; }

        [DataMember]
        public EntityState EntityState { get; private set; }

        [DataMember]
        private Dictionary<string, PropertyData> PropertyDatas { get; } = new Dictionary<string, PropertyData>();

        public IEnumerable<JoinedProperty> YieldProperty(IProperty efProperty)
        {
            PropertyData data;
            if (this.PropertyDatas.TryGetValue(efProperty.Name, out data))
            {
                yield return new JoinedProperty { EfProperty = efProperty, DtoProperty = data };
            }
        }

        public class JoinedProperty
        {
            public IProperty EfProperty { get; set; }

            public PropertyData DtoProperty { get; set; }
        }

        [DataContract]
        public class PropertyData
        {
            private static readonly DynamicObjectMapper Mapper
                = new DynamicObjectMapper(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true });

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
            public bool HasTemporaryValue { get; set; }

            [DataMember]
            public bool IsStoreGenerated { get; set; }
        }
    }
}
