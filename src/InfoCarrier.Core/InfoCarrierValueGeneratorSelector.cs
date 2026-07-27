// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side value generator selector. Numeric keys get a temporary generator so the
///     client can track <c>Added</c> entities before SaveChanges; the server assigns the real
///     store-generated keys during replay and they flow back (requirements §2.2). This mirrors
///     v1 — the client never suppresses key generation in the shared model.
/// </summary>
public class InfoCarrierValueGeneratorSelector : ValueGeneratorSelector
{
    private readonly TemporaryNumberValueGeneratorFactory _numberFactory = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierValueGeneratorSelector" /> class.
    /// </summary>
    public InfoCarrierValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <inheritdoc />
    public override bool TryCreate(IProperty property, ITypeBase typeBase, out ValueGenerator? valueGenerator)
    {
        if (property.ValueGenerated != ValueGenerated.Never)
        {
            Type propertyType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
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
                valueGenerator = _numberFactory.Create(property, typeBase);
                return true;
            }
        }

        return base.TryCreate(property, typeBase, out valueGenerator);
    }
}
