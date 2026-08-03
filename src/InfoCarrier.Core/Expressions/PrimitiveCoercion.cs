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
    ///     Converts a mapped value to the form it travels in: the <em>provider</em> value,
    ///     whenever the property has a value converter.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A value behind a converter is an arbitrary CLR type, and the wire has two ways of
    ///         failing on one. A <em>key</em> lands in <see cref="EntityKeyNode.KeyValues" />,
    ///         declared <see cref="object" />, where the source-generated serializer resolves
    ///         <c>JsonTypeInfo</c> by runtime type and has none — all 40 of
    ///         `KeysWithConvertersTestBase` failed on that alone. An ordinary <em>property</em>
    ///         falls through to the mapper's reflective member walk, which reads every public
    ///         getter: `ValueConvertersEndToEndTestBase` stores an <see cref="System.Net.IPAddress" />,
    ///         whose <c>ScopeId</c> throws <c>SocketException</c> for an IPv4 address.
    ///     </para>
    ///     <para>
    ///         Both go away for the same reason. The provider value is what the store itself
    ///         holds, is by construction one of the registered primitives, and both sides of the
    ///         wire compute it from the model — so it is what travels. This is what ADR-008
    ///         constraint 1 means by reading a scalar through its <c>IProperty</c>: honouring the
    ///         converter, not merely going through the accessor.
    ///     </para>
    /// </remarks>
    public static object? ToWireValue(Microsoft.EntityFrameworkCore.Metadata.IProperty property, object? value)
    {
        if (property.GetValueConverter() is { } converter)
        {
            return Normalize(converter.ConvertToProvider(value));
        }

        return value is not null && JsonForm(property) is { } writer
            ? writer.ToJsonString(value)
            : Normalize(value);
    }

    /// <summary>
    ///     The type a value of <paramref name="property" /> travels as.
    /// </summary>
    public static Type WireType(Microsoft.EntityFrameworkCore.Metadata.IProperty property)
        => property.GetValueConverter()?.ProviderClrType
            ?? (JsonForm(property) is not null ? typeof(string) : property.ClrType);

    /// <summary>
    ///     The inverse of <see cref="ToWireValue" />: a wire value as its CLR type.
    /// </summary>
    public static object? FromWireValue(Microsoft.EntityFrameworkCore.Metadata.IProperty property, object? value)
    {
        if (property.GetValueConverter() is { } converter)
        {
            return converter.ConvertFromProvider(Coerce(value, converter.ProviderClrType));
        }

        return JsonForm(property) is { } reader && Coerce(value, typeof(string)) is string json
            ? reader.FromJsonString(json)
            : Coerce(value, property.ClrType);
    }

    /// <summary>
    ///     Whether a property with no value converter still holds something the wire has no
    ///     primitive form for — in which case EF's own JSON form is what travels.
    /// </summary>
    /// <remarks>
    ///     A converter is not the only way a mapped value can be an arbitrary CLR type. The
    ///     InMemory store keeps <c>Faction.ServerAddress</c> as a live
    ///     <see cref="System.Net.IPAddress" /> with no conversion at all, and that fell through to
    ///     the mapper's reflective member walk — where <c>ScopeId</c> throws
    ///     <c>SocketException</c> for an IPv4 address, the same signature this file's converter
    ///     rule was written for, 30 times over <c>GearsOfWarQueryTestBase</c>.
    ///     <para>
    ///         The model already answers what to do with such a value: EF gives the property a
    ///         <c>JsonValueReaderWriter</c> precisely because it knows how to write it. Using that
    ///         is not a guess, and it is symmetric — the same reader rebuilds it on the far side.
    ///     </para>
    ///     <para>
    ///         Read off the <em>type mapping</em>, not off
    ///         <c>IReadOnlyProperty.GetJsonValueReaderWriter()</c>: that one answers only what the
    ///         model was explicitly annotated with, which is nothing in the ordinary case. Using it
    ///         made this rule measure byte-identical — the code ran and the condition was simply
    ///         never true.
    ///     </para>
    /// </remarks>
    private static Microsoft.EntityFrameworkCore.Storage.Json.JsonValueReaderWriter? JsonForm(
        Microsoft.EntityFrameworkCore.Metadata.IProperty property)
        => IsWirePrimitive(property.ClrType)
            ? null
            : property.GetJsonValueReaderWriter() ?? property.FindTypeMapping()?.JsonValueReaderWriter;

    private static bool IsWirePrimitive(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
            || underlying == typeof(byte[]);
    }

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
