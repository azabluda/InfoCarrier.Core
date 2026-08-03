// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Globalization;
using System.Text.Json;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Conversion of primitive values across the wire boundary, shared by
///     <see cref="NodeToExpressionTranslator" /> (constants) and
///     <see cref="DynamicValueMapper" /> (scalar values and object properties).
/// </summary>
/// <remarks>
///     Both directions are needed because a primitive can reach the far side either as its
///     own CLR value (in-process transport) or as a <see cref="JsonElement" /> (after a real
///     serialization round-trip). Enums always travel as their underlying integral value —
///     see <c>ExpressionToNodeTranslator.NormalizePrimitive</c> — and are rebuilt here from
///     the declared target type.
/// </remarks>
internal static class PrimitiveCoercion
{
    /// <summary>
    ///     Converts an enum value to its underlying integral value for the wire; passes any
    ///     other value through unchanged. A concrete enum type can never be pre-registered in
    ///     the source-generated serializer context (see <see cref="ExpressionJsonContext" />).
    /// </summary>
    public static object? Normalize(object? value)
        => value is not null && value.GetType().IsEnum
            ? Convert.ChangeType(
                value,
                Enum.GetUnderlyingType(value.GetType()),
                CultureInfo.InvariantCulture)
            : value;

    /// <summary>
    ///     Converts a key value to the form it travels in: the <em>provider</em> value, when the
    ///     property has a value converter.
    /// </summary>
    /// <remarks>
    ///     A key behind a converter is an arbitrary CLR type — <c>ComparableBytesStructKey</c>,
    ///     <c>IntClassKey</c> — and <see cref="EntityKeyNode.KeyValues" /> is declared
    ///     <see cref="object" />, so the source-generated serializer resolves its
    ///     <c>JsonTypeInfo</c> by runtime type and has none: every one of
    ///     `KeysWithConvertersTestBase`'s 40 tests failed on that alone. The converter's provider
    ///     value is what the store keys on, is by construction one of the registered primitives,
    ///     and both sides of the wire can compute it from the model.
    /// </remarks>
    public static object? ToWireKey(Microsoft.EntityFrameworkCore.Metadata.IProperty property, object? value)
        => Normalize(property.GetValueConverter() is { } converter ? converter.ConvertToProvider(value) : value);

    /// <summary>
    ///     The inverse of <see cref="ToWireKey" />: a wire key value as its CLR type.
    /// </summary>
    public static object? FromWireKey(Microsoft.EntityFrameworkCore.Metadata.IProperty property, object? value)
        => property.GetValueConverter() is { } converter
            ? converter.ConvertFromProvider(Coerce(value, converter.ProviderClrType))
            : Coerce(value, property.ClrType);

    /// <summary>
    ///     Converts a wire-side primitive back to <paramref name="targetType" />.
    /// </summary>
    public static object? Coerce(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is JsonElement element)
        {
            if (element.ValueKind is JsonValueKind.Null)
            {
                return null;
            }

            if (underlying.IsEnum)
            {
                // GetInt64 (not GetInt32) so enums with long/ulong backing survive.
                return Enum.ToObject(underlying, element.GetInt64());
            }

            if (underlying == typeof(string)) return element.GetString();
            if (underlying == typeof(bool)) return element.GetBoolean();
            if (underlying == typeof(int)) return element.GetInt32();
            if (underlying == typeof(uint)) return element.GetUInt32();
            if (underlying == typeof(long)) return element.GetInt64();
            if (underlying == typeof(ulong)) return element.GetUInt64();
            if (underlying == typeof(short)) return element.GetInt16();
            if (underlying == typeof(ushort)) return element.GetUInt16();
            if (underlying == typeof(byte)) return element.GetByte();
            if (underlying == typeof(sbyte)) return element.GetSByte();
            if (underlying == typeof(double)) return element.GetDouble();
            if (underlying == typeof(float)) return element.GetSingle();
            if (underlying == typeof(decimal)) return element.GetDecimal();
            if (underlying == typeof(char)) return element.GetString() is { Length: > 0 } s ? s[0] : default(char);
            if (underlying == typeof(Guid)) return element.GetGuid();
            if (underlying == typeof(DateTime)) return element.GetDateTime();
            if (underlying == typeof(DateTimeOffset)) return element.GetDateTimeOffset();
            if (underlying == typeof(DateOnly)) return DateOnly.Parse(element.GetString()!, CultureInfo.InvariantCulture);
            if (underlying == typeof(TimeOnly)) return TimeOnly.Parse(element.GetString()!, CultureInfo.InvariantCulture);
            if (underlying == typeof(TimeSpan)) return TimeSpan.Parse(element.GetString()!, CultureInfo.InvariantCulture);
            if (underlying == typeof(object)) return element;
            if (underlying == typeof(byte[])) return element.GetBytesFromBase64();

            // A JSON string standing where something broader is declared. `AsEnumerable()` over
            // a string member types it `IEnumerable<char>`, which no branch above matches and
            // which STJ cannot build from a JSON string — a `string` satisfies it directly.
            if (element.ValueKind is JsonValueKind.String && underlying.IsAssignableFrom(typeof(string)))
            {
                return element.GetString();
            }

            return JsonSerializer.Deserialize(element.GetRawText(), underlying);
        }

        // Already a CLR value (in-process transport, or a type the serializer preserved).
        if (underlying.IsEnum)
        {
            return Enum.ToObject(underlying, value);
        }

        return underlying.IsInstanceOfType(value)
            ? value
            : Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
    }
}
