namespace InfoCarrier.Core.Client.ValueGeneration.Internal
{
    using System;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.ValueGeneration;
    using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
    using Utils;

    public class InfoCarrierValueGeneratorSelector : ValueGeneratorSelector
    {
        private readonly TemporaryNumberValueGeneratorFactory numberFactory
            = new TemporaryNumberValueGeneratorFactory();

        public InfoCarrierValueGeneratorSelector(IValueGeneratorCache cache)
            : base(cache)
        {
        }

        public override ValueGenerator Create(IProperty property, IEntityType entityType)
        {
            Type propertyType = property.ClrType.UnwrapNullableType().UnwrapEnumType();

            if (propertyType.IsInteger()
                || (propertyType == typeof(decimal))
                || (propertyType == typeof(float))
                || (propertyType == typeof(double)))
            {
                return this.numberFactory.Create(property);
            }

            if (propertyType == typeof(string))
            {
                return new StringValueGenerator(generateTemporaryValues: true);
            }

            if (propertyType == typeof(byte[]))
            {
                return new BinaryValueGenerator(generateTemporaryValues: true);
            }

            if (propertyType == typeof(DateTime))
            {
                return new TemporaryDateTimeOffsetValueGenerator();
            }

            if (propertyType == typeof(DateTimeOffset))
            {
                return new TemporaryDateTimeOffsetValueGenerator();
            }

            return base.Create(property, entityType);
        }
    }
}
