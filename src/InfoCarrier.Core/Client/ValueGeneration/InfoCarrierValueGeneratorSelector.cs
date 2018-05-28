// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.ValueGeneration
{
    using System;
    using System.Diagnostics.CodeAnalysis;
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
            if (property.ValueGenerated != ValueGenerated.Never)
            {
                var propertyType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (propertyType == typeof(int)
                    || propertyType == typeof(long)
                    || propertyType == typeof(short)
                    || propertyType == typeof(byte)
                    || propertyType == typeof(uint)
                    || propertyType == typeof(ulong)
                    || propertyType == typeof(ushort)
                    || propertyType == typeof(sbyte)
                    || propertyType == typeof(char)
                    || propertyType == typeof(decimal)
                    || propertyType == typeof(float)
                    || propertyType == typeof(double))
                {
                    return this.numberFactory.Create(property);
                }
            }

            return base.Create(property, entityType);
        }
    }
}
