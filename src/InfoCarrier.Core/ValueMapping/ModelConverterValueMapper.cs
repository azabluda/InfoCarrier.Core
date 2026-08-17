// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InfoCarrier.Core.ValueMapping;

/// <summary>
///     Maps a CLR type the <b>model</b> already declares a value converter for onto the primitive
///     that converter produces (M9 J9, [ADR-012](../../../docs/decisions.md) as amended
///     2026-08-17).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this is allowed to consult the model, when ADR-012 says a mapper must decide from
///         the CLR type alone.</b> That clause bars a <em>store</em> type mapping, which the client
///         and the server compute independently and which therefore has two answers — B23 measured
///         the cost of ignoring that at <b>381</b>. A converter declared in <c>OnModelCreating</c>
///         is not one: it is shared configuration, identical on both halves by construction, and it
///         is the same fact B12/C80 and J5 already require both sides to agree about. The dated
///         amendment in <c>decisions.md</c> states exactly this and nothing wider.
///     </para>
///     <para>
///         <b>What it fixes.</b> A query constant whose CLR type is a key behind a converter had no
///         way across. Outbound, <c>EnumerableClassKey</c> implements <see cref="IEnumerable" /> so
///         the reflective walk took its collection branch and <c>GetEnumerator()</c> threw;
///         inbound, <c>Key(string id)</c> has no parameterless constructor and the server could not
///         rebuild it. In both cases the model already said the value is a <see cref="string" />;
///         the wire simply never asked.
///     </para>
///     <para>
///         <b>Symmetry is structural rather than a registration rule.</b> This mapper is built
///         inside <see cref="Expressions.DynamicValueMapper" /> from whichever model that mapper was
///         given, so each half derives it from its own model — and the two models agree about
///         converters by construction (A49). Unlike the application-registered mappers, it cannot
///         be present on one side only.
///     </para>
///     <para>
///         <b>Deliberately last in the chain.</b> An application's own mapper for a type keeps
///         first refusal, so registering one is never overridden by the model's opinion.
///     </para>
/// </remarks>
public sealed class ModelConverterValueMapper : IInfoCarrierValueMapper
{
    private readonly Dictionary<Type, ValueConverter> _byClrType;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelConverterValueMapper" /> class from
    ///     the converters <paramref name="model" /> declares.
    /// </summary>
    /// <param name="model">The model to read converters from.</param>
    public ModelConverterValueMapper(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _byClrType = [];

        foreach (IEntityType entityType in model.GetEntityTypes())
        {
            foreach (IProperty property in entityType.GetProperties())
            {
                if (property.GetValueConverter() is not { } converter)
                {
                    continue;
                }

                Type modelType = Underlying(converter.ModelClrType);
                Type providerType = Underlying(converter.ProviderClrType);

                // Only where the wire has no better answer already: a model type that is not
                // itself a wire primitive, converted *to* one. A converter between two primitives
                // is `PrimitiveCoercion`'s business and short-circuits before any mapper runs, and
                // widening past that is precisely B23's mistake.
                if (!IsWirePrimitive(modelType) && IsWirePrimitive(providerType))
                {
                    _byClrType[modelType] = converter;
                }
            }
        }
    }

    /// <summary>
    ///     Whether this mapper has anything at all to say for the given model.
    /// </summary>
    /// <remarks>
    ///     Lets the chain skip it entirely for the overwhelmingly common model that converts
    ///     nothing non-primitive — which is every model in this suite except two.
    /// </remarks>
    public bool IsEmpty => _byClrType.Count == 0;

    /// <inheritdoc />
    public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
    {
        ArgumentNullException.ThrowIfNull(value);

        wireValue = null;

        if (declaredType is null || !_byClrType.TryGetValue(Underlying(declaredType), out ValueConverter? converter))
        {
            return false;
        }

        wireValue = converter.ConvertToProvider(value);

        // A converter that answers null has said nothing the reverse direction could use, so
        // decline rather than claim it — the contract requires a non-null wire value.
        return wireValue is not null;
    }

    /// <inheritdoc />
    public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
    {
        value = null;

        if (declaredType is null
            || wireValue is null
            || !_byClrType.TryGetValue(Underlying(declaredType), out ValueConverter? converter))
        {
            return false;
        }

        // After a serialization round trip a wire primitive arrives as a `JsonElement`, exactly as
        // it does for every other mapper — convert it rather than casting it.
        object provider = wireValue is JsonElement element
            ? Read(element, Underlying(converter.ProviderClrType))
            : wireValue;

        value = converter.ConvertFromProvider(
            Convert.ChangeType(provider, Underlying(converter.ProviderClrType), System.Globalization.CultureInfo.InvariantCulture));
        return true;
    }

    private static object Read(JsonElement element, Type providerType)
        => element.ValueKind switch
        {
            JsonValueKind.String when providerType == typeof(byte[]) => element.GetBytesFromBase64(),
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            _ => throw new InvalidOperationException(
                $"A converted value arrived on the wire as '{element.ValueKind}', which no "
                    + "provider type this mapper handles can be read from."),
        };

    private static Type Underlying(Type type)
        => Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>
    ///     The primitives the wire carries directly. Kept deliberately short: the point is to
    ///     recognise a converter that lands on one, not to enumerate everything serializable.
    /// </summary>
    private static bool IsWirePrimitive(Type type)
        => type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(byte[])
            || type == typeof(Guid)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(TimeSpan);
}
