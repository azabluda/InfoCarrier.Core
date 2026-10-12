// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.ValueMapping;

public sealed class LegacyMapper : IInfoCarrierValueMapper
{
    public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
    {
        wireValue = null;
        return false;
    }

    public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
    {
        value = null;
        return false;
    }
}
