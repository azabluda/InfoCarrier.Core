// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.ValueGeneration
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.ValueGeneration;
    using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InfoCarrierValueGeneratorSelector : ValueGeneratorSelector
    {
        private readonly TemporaryNumberValueGeneratorFactory numberFactory
            = new TemporaryNumberValueGeneratorFactory();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1642:ConstructorSummaryDocumentationMustBeginWithStandardText", Justification = "Entity Framework Core internal.")]
        public InfoCarrierValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies)
            : base(dependencies)
        {
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public override ValueGenerator Create(IProperty property, IEntityType entityType)
        {
            Type UnwrapNullableType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

            bool IsNullableType(Type type)
            {
                var typeInfo = type.GetTypeInfo();

                return !typeInfo.IsValueType
                       || (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(Nullable<>));
            }

            Type MakeNullable(Type type)
                => IsNullableType(type)
                    ? type
                    : typeof(Nullable<>).MakeGenericType(type);

            bool IsInteger(Type type)
            {
                type = UnwrapNullableType(type);

                return type == typeof(int)
                       || type == typeof(long)
                       || type == typeof(short)
                       || type == typeof(byte)
                       || type == typeof(uint)
                       || type == typeof(ulong)
                       || type == typeof(ushort)
                       || type == typeof(sbyte)
                       || type == typeof(char);
            }

            Type UnwrapEnumType(Type type)
            {
                var isNullable = IsNullableType(type);
                var underlyingNonNullableType = isNullable ? UnwrapNullableType(type) : type;
                if (!underlyingNonNullableType.GetTypeInfo().IsEnum)
                {
                    return type;
                }

                var underlyingEnumType = Enum.GetUnderlyingType(underlyingNonNullableType);
                return isNullable ? MakeNullable(underlyingEnumType) : underlyingEnumType;
            }

            if (property.ValueGenerated != ValueGenerated.Never)
            {
                var propertyType = UnwrapEnumType(UnwrapNullableType(property.ClrType));

                if (IsInteger(propertyType)
                    || propertyType == typeof(decimal)
                    || propertyType == typeof(float)
                    || propertyType == typeof(double))
                {
                    return this.numberFactory.Create(property);
                }

                if (propertyType == typeof(DateTime))
                {
                    return new TemporaryDateTimeValueGenerator();
                }

                if (propertyType == typeof(DateTimeOffset))
                {
                    return new TemporaryDateTimeOffsetValueGenerator();
                }
            }

            return base.Create(property, entityType);
        }
    }
}
