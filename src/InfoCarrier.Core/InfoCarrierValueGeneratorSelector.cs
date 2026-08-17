// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side value generator selector. Numeric keys get a temporary generator so the
///     client can track <c>Added</c> entities before SaveChanges; the server assigns the real
///     store-generated keys during replay and they flow back (requirements §2.2). This mirrors
///     v1 — the client never suppresses key generation in the shared model.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="InfoCarrierValueGeneratorSelector" /> class.
/// </remarks>
public class InfoCarrierValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies) : ValueGeneratorSelector(dependencies)
{
    private readonly TemporaryNumberValueGeneratorFactory _numberFactory = new();

    /// <inheritdoc />
    public override bool TryCreate(IProperty property, ITypeBase typeBase, out ValueGenerator? valueGenerator)
    {
        if (property.ValueGenerated != ValueGenerated.Never
            && IsTemporaryNumber(property, out ValueConverter? throughConverter))
        {
            ValueGenerator generated = _numberFactory.Create(property, typeBase);

            // When the number was found through a converter, the factory produced a generator of
            // the *provider* type and EF will store its output as the model type. Without the
            // wrap, a `WrappedIntKeyClass` key got a raw `int` and EF's own snapshotting failed —
            // "Unable to cast object of type 'System.Int32' to type 'WrappedIntKeyClass'", inside
            // `ValueComparer.Snapshot`. EF's core selector wraps for the same reason.
            valueGenerator = throughConverter is null ? generated : generated.WithConverter(throughConverter);
            return true;
        }

        return base.TryCreate(property, typeBase, out valueGenerator);
    }

    /// <summary>
    ///     Whether <see cref="TemporaryNumberValueGeneratorFactory" /> can produce a generator for
    ///     this property — asked exactly the way that factory answers it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The guard used to test <c>property.ClrType</c> against a list of numeric types,
    ///         which is narrower than the factory in two ways, and a key of either shape reached
    ///         <c>base.TryCreate</c> and failed at <c>context.Add</c> with <i>"no value generator
    ///         is available for properties of type 'KeyEnum'"</i> — before the wire, so no amount
    ///         of server work could have helped.
    ///     </para>
    ///     <list type="number">
    ///         <item>
    ///             <b>An enum key.</b> <c>KeyEnum</c> is backed by an <c>int</c> and the factory
    ///             unwraps it; the old list did not.
    ///         </item>
    ///         <item>
    ///             <b>A key behind a value converter.</b> <c>WrappedIntKeyClass</c> is a class
    ///             whose provider type is <c>int</c>. The factory already looks through
    ///             <c>Converter.ProviderClrType</c> — EF's own core selector does the same — so
    ///             the generator exists; only this guard was refusing to ask for it.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         Asking through the <em>type mapping</em> rather than the CLR type is the same
    ///         choice the factory makes, and keeps the two from disagreeing.
    ///     </para>
    /// </remarks>
    private static bool IsTemporaryNumber(IProperty property, out ValueConverter? throughConverter)
    {
        Microsoft.EntityFrameworkCore.Storage.CoreTypeMapping mapping = property.GetTypeMapping();
        throughConverter = null;

        if (IsNumber(Underlying(mapping.ClrType)))
        {
            return true;
        }

        if (mapping.Converter is { } converter && IsNumber(Underlying(converter.ProviderClrType)))
        {
            throughConverter = converter;
            return true;
        }

        return false;

        static Type Underlying(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsEnum ? Enum.GetUnderlyingType(type) : type;
        }

        static bool IsNumber(Type type)
            => type == typeof(int)
                || type == typeof(long)
                || type == typeof(short)
                || type == typeof(byte)
                || type == typeof(uint)
                || type == typeof(ulong)
                || type == typeof(ushort)
                || type == typeof(sbyte)
                || type == typeof(char)
                || type == typeof(decimal)
                || type == typeof(float)
                || type == typeof(double);
    }
}
