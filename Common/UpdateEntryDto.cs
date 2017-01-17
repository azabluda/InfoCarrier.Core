namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections;
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
        internal Dictionary<string, PropertyData> PropertyDatas { get; } = new Dictionary<string, PropertyData>();

        public IEnumerable<Property> YieldPropery(IProperty efProperty)
        {
            PropertyData data;
            if (this.PropertyDatas.TryGetValue(efProperty.Name, out data))
            {
                yield return new Property(efProperty, data);
            }
        }

        public class Property
        {
            private readonly PropertyData data;

            internal Property(IProperty efProperty, PropertyData data)
            {
                this.EfProperty = efProperty;
                this.data = data;
            }

            public IProperty EfProperty { get; }

            public object OriginalValue => Converter.Convert(this.data.OriginalValue, this.EfProperty.ClrType);

            public object CurrentValue => Converter.Convert(this.data.CurrentValue, this.EfProperty.ClrType);

            public bool HasTemporaryValue => this.data.HasTemporaryValue;

            public bool IsModified => this.data.IsModified;
        }

        private sealed class Converter : DynamicObjectMapper
        {
            private static readonly Converter Instance = new Converter();

            private static object PrepareForConversion(object obj)
            {
                if (obj == null || obj is string)
                {
                    return obj;
                }

                var coll = obj as IEnumerable;
                if (coll != null)
                {
                    return coll.Cast<object>().Select(PrepareForConversion).ToList();
                }

                return obj.ToString();
            }

            public static object Convert(object obj, Type type)
            {
                return Instance.MapFromDynamicObjectGraph(PrepareForConversion(obj), type);
            }
        }

        [DataContract]
        internal class PropertyData
        {
            [DataMember]
            public object OriginalValue { get; set; }

            [DataMember]
            public object CurrentValue { get; set; }

            [DataMember]
            public bool IsModified { get; set; }

            [DataMember]
            public bool HasTemporaryValue { get; set; }

            [DataMember]
            public bool IsStoreGenerated { get; set; }
        }
    }
}
