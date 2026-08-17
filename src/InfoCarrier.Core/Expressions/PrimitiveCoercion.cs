// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.Json;

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
        if (EffectiveConverter(property) is { } converter)
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
        => EffectiveConverter(property)?.ProviderClrType
            ?? (JsonForm(property) is not null ? typeof(string) : property.ClrType);

    /// <summary>
    ///     The inverse of <see cref="ToWireValue" />: a wire value as its CLR type.
    /// </summary>
    public static object? FromWireValue(Microsoft.EntityFrameworkCore.Metadata.IProperty property, object? value)
    {
        if (EffectiveConverter(property) is { } converter)
        {
            return converter.ConvertFromProvider(Coerce(value, converter.ProviderClrType));
        }

        return JsonForm(property) is { } reader && Coerce(value, typeof(string)) is string json
            ? reader.FromJsonString(json)
            : Coerce(value, property.ClrType);
    }

    /// <summary>
    ///     The value converter that actually applies to <paramref name="property" /> — which is
    ///     <em>not</em> always the one <c>GetValueConverter()</c> returns.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>A compiled model is where the two part company (C91).</b>
    ///         <c>CSharpRuntimeModelCodeGenerator</c> emits a <c>valueConverter:</c> constructor
    ///         argument only when it can name a converter <em>type</em>. Under
    ///         <c>ForNativeAot</c> — which is what <c>dotnet ef dbcontext optimize</c> generates —
    ///         it puts the converter on the property's <b>type mapping</b> instead, so
    ///         <c>GetValueConverter()</c> is <see langword="null" /> for every property configured
    ///         with a converter <em>instance</em>: <c>HasConversion(new BoolToStringConverter("A",
    ///         "B"))</c> loses it where <c>HasConversion&lt;BoolToZeroOneConverter&lt;short&gt;&gt;()</c>
    ///         keeps it. The client then sends the <em>model</em> value where the server expects
    ///         the <em>provider</em> value, and `BigModel` said so as
    ///         <c>Invalid cast from 'System.String' to 'System.Byte[]'</c>.
    ///     </para>
    ///     <para>
    ///         <b>Guarded by the mapping's own type, and a plain <c>?? FindTypeMapping()?.Converter</c>
    ///         would be wrong.</b> A type mapping is computed twice, by two different providers,
    ///         and a <em>store's</em> mapping may carry a converter the model never asked for —
    ///         SQLite maps <see cref="DateTimeOffset" /> through one. Falling back to the mapping
    ///         on the server would newly apply a store conversion the client never applied, in
    ///         both directions. An <see cref="InfoCarrierTypeMapping" /> is safe because it is the
    ///         <em>client's</em>, and this provider's type mapping source never composes a
    ///         converter of its own: one is there only if the model put it there.
    ///     </para>
    ///     <para>
    ///         <b>Two more guards, and a probe found both by printing what each side computes for
    ///         every property and diffing the two.</b> The first version of this measured 23
    ///         disagreements where it meant to close 3.
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <b>A provider CLR type means the model asked for a target, not for a
    ///             converter.</b> <c>HasConversion&lt;string&gt;()</c> on an enum sets
    ///             <c>ProviderClrType</c> and no converter annotation, so a <em>built</em> model —
    ///             which is what the server has — answers <c>GetValueConverter() == null</c> and
    ///             sends the raw enum. The mapping on either side does hold a converter; taking it
    ///             on the client alone made the server read <c>"Enum8"</c> as a number.
    ///         </item>
    ///         <item>
    ///             <b>A primitive collection is <see cref="JsonForm" />'s, not a converter's.</b>
    ///             A compiled model's mapping carries EF's <c>CollectionToJsonStringConverter</c>,
    ///             and that is the mapping's own business rather than model configuration —
    ///             <see cref="CollectionForm" /> exists precisely so a collection's JSON form is
    ///             derived store-independently (B4). Twenty-one of the twenty-three disagreements
    ///             were this.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         So this changes nothing for a model that was built rather than compiled — there
    ///         <c>GetValueConverter()</c> is non-null whenever a converter exists and the fallback
    ///         never fires.
    ///     </para>
    /// </remarks>
    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter? EffectiveConverter(
        Microsoft.EntityFrameworkCore.Metadata.IProperty property)
        => property.GetValueConverter()
            ?? (property.GetProviderClrType() is null && JsonForm(property) is null
                ? (property.FindTypeMapping() as InfoCarrierTypeMapping)?.Converter
                : null);

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
            : CollectionForm(property.ClrType)
                ?? property.GetJsonValueReaderWriter()
                ?? property.FindTypeMapping()?.JsonValueReaderWriter;

    /// <summary>
    ///     The <em>store-independent</em> JSON form of a collection of wire primitives, or
    ///     <see langword="null" /> if the type is not one.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A JSON form read off <c>FindTypeMapping()</c> is the <em>backing store's</em>, and
    ///         the two ends of the wire do not have the same one. The server's model is built by
    ///         SQLite, whose <c>DateTime</c> element writes <c>2023-01-01 12:30:00</c>; the client's
    ///         is built by this provider, whose reader is EF's core <c>JsonDateTimeReaderWriter</c>
    ///         and wants ISO-8601. It threw <c>FormatException: The JSON value is not in a supported
    ///         DateTime format</c> — 101 of the 132 failures `PrimitiveCollectionsQuery` opened with,
    ///         plus 6 of `NonSharedPrimitiveCollectionsQuery`'s 7, in both directions, since
    ///         <c>SaveChanges</c> runs the mirror image. `decimal` is the same shape: SQLite writes
    ///         the JSON string <c>'1.0'</c> where the core reader wants a number.
    ///     </para>
    ///     <para>
    ///         For a scalar this never came up, because every store agrees on the wire primitives
    ///         and <see cref="IsWirePrimitive" /> short-circuits them. A <em>collection</em> of them
    ///         is not itself a wire primitive, so it fell through to the mapping — and a mapping is
    ///         exactly the thing the two sides are entitled to disagree about.
    ///     </para>
    ///     <para>
    ///         The form is therefore derived from the <b>CLR type alone</b>, through EF's own
    ///         core <see cref="JsonValueReaderWriterSource" /> (which no provider replaces) and its
    ///         own collection wrappers. Deriving it from the model instead — <c>GetElementType()</c>
    ///         — would reintroduce the asymmetry it fixes, since that is a modelling answer each
    ///         side computes for itself. An element the core source does not know, which includes
    ///         every element behind a value converter, falls through to the old path unchanged.
    ///     </para>
    /// </remarks>
    private static Microsoft.EntityFrameworkCore.Storage.Json.JsonValueReaderWriter? CollectionForm(Type collectionType)
        => CollectionForms.GetOrAdd(collectionType, BuildCollectionForm);

    private static readonly JsonValueReaderWriterSource CoreReaderWriters = new(new JsonValueReaderWriterSourceDependencies());

    private static readonly ConcurrentDictionary<Type, JsonValueReaderWriter?> CollectionForms = new();

    private static JsonValueReaderWriter? BuildCollectionForm(Type collectionType)
    {
        if (ElementTypeOf(collectionType) is not { } elementType)
        {
            return null;
        }

        Type underlying = Nullable.GetUnderlyingType(elementType) ?? elementType;

        if (CoreReaderWriters.FindReaderWriter(underlying) is not { } elementReaderWriter)
        {
            return null;
        }

        Type openForm = underlying != elementType
            ? typeof(JsonCollectionOfNullableStructsReaderWriter<,>)
            : elementType.IsValueType
                ? typeof(JsonCollectionOfStructsReaderWriter<,>)
                : typeof(JsonCollectionOfReferencesReaderWriter<,>);

        return (JsonValueReaderWriter?)Activator.CreateInstance(
            openForm.MakeGenericType(ConcreteCollectionType(collectionType, elementType), underlying),
            elementReaderWriter);
    }

    /// <summary>
    ///     The element type of a collection of primitives, or <see langword="null" />.
    /// </summary>
    /// <remarks>
    ///     <see cref="string" /> and <see cref="byte" /><c>[]</c> are enumerable and are wire
    ///     primitives in their own right; a dictionary is enumerable and is not a collection of its
    ///     values. EF's own <c>TryFindJsonCollectionMapping</c> excludes the same three.
    /// </remarks>
    private static Type? ElementTypeOf(Type collectionType)
    {
        if (collectionType == typeof(string)
            || collectionType == typeof(byte[])
            || collectionType.GetInterfaces().Any(
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
        {
            return null;
        }

        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        foreach (Type candidate in collectionType.GetInterfaces().Prepend(collectionType))
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return candidate.GetGenericArguments()[0];
            }
        }

        return null;
    }

    /// <summary>
    ///     The type EF's collection reader/writer instantiates, copied from
    ///     <c>TypeMappingSourceBase.TryFindJsonCollectionMapping</c>: the declared type when it can
    ///     be constructed, <c>List&lt;T&gt;</c> when it cannot.
    /// </summary>
    private static Type ConcreteCollectionType(Type collectionType, Type elementType)
    {
        if (collectionType.IsArray)
        {
            return collectionType;
        }

        Type listOfElement = typeof(List<>).MakeGenericType(elementType);

        if (!collectionType.IsAssignableFrom(listOfElement))
        {
            return collectionType;
        }

        return !collectionType.IsAbstract
            && collectionType.GetConstructor(Type.EmptyTypes) is { IsPublic: true }
                ? collectionType
                : listOfElement;
    }

    /// <summary>
    ///     Whether the wire carries a value of this type as a primitive, with no mapping consulted.
    /// </summary>
    /// <remarks>
    ///     <c>internal</c> rather than private since M9 J21, which needs the same question one
    ///     layer up: <c>QueryExecutor.Substitute</c> decides whether a parameter value may be
    ///     inlined as a constant, and "is it a wire primitive" is exactly the line.
    /// </remarks>
    internal static bool IsWirePrimitive(Type type)
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
            // `NaN`, `Infinity` and `-Infinity` have no JSON number literal, so
            // `ExpressionJsonContext`'s `AllowNamedFloatingPointLiterals` writes them as JSON
            // *strings*. `GetDouble()` on a string element throws ("requires an element of type
            // 'Number'"), so the named form has to be read back explicitly — the writing half
            // alone turns "cannot be written" into "cannot be read", which is no better.
            if (underlying == typeof(double))
            {
                return element.ValueKind is JsonValueKind.String
                    ? double.Parse(element.GetString()!, CultureInfo.InvariantCulture)
                    : element.GetDouble();
            }

            if (underlying == typeof(float))
            {
                return element.ValueKind is JsonValueKind.String
                    ? float.Parse(element.GetString()!, CultureInfo.InvariantCulture)
                    : element.GetSingle();
            }

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
