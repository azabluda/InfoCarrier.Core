// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     A concrete type mapping for client-side CLR primitives. No store conversion — the server
///     owns the real store mapping.
/// </summary>
/// <remarks>
///     <para>
///         <b>It is a <see cref="RelationalTypeMapping" /> and its store type is a fiction.</b>
///         The client has no database, so no true store type name exists here. EF's relational
///         surface asks for one anyway, and code an application can legitimately reach — EF's own
///         <c>SqlQueryTestBase</c> does exactly this — casts what
///         <c>ITypeMappingSource.FindMapping</c> returns to <see cref="RelationalTypeMapping" />.
///         With a core mapping that cast throws, which is a worse answer than a neutral one.
///     </para>
///     <para>
///         <b>The name is derived from the CLR type and from nothing else</b>, which is the rule
///         this repository already applies to everything the wire computes from a mapping: the
///         client's model and the server's are built by two different providers, so any value
///         taken from one provider's opinion can disagree with the other's. A name derived from
///         the CLR type cannot, because both sides see the same CLR type. It is also the reason
///         no store-specific spelling appears here: <c>VARCHAR2</c> belongs to a backend, and a
///         shared model must not carry one.
///     </para>
///     <para>
///         <b>What this does NOT make true.</b> <see cref="RelationalTypeMapping.GenerateSqlLiteral" />
///         now answers on the client, and its answer is EF's generic form rather than any store's.
///         For a value whose literal syntax every SQL dialect shares, that is the right text; for
///         one where dialects differ, it is a guess, and a caller composing raw SQL owns that
///         choice the same way they own the rest of the statement. Nothing in this provider calls
///         it: the wire carries values, never literals.
///     </para>
/// </remarks>
public class InfoCarrierTypeMapping : RelationalTypeMapping
{
    // DECLARED FIRST BECAUSE `Default` BELOW IS BUILT FROM IT. Static field initializers
    // run in textual order, and `Default` calls the constructor, which reads this table:
    // declared after it, the table is still null when the type initializer runs and every
    // client context in the process fails to build a model.
    private static readonly Dictionary<Type, RelationalTypeMapping> Neutral = new()
    {
        [typeof(bool)] = BoolTypeMapping.Default,
        [typeof(byte)] = ByteTypeMapping.Default,
        [typeof(byte[])] = ByteArrayTypeMapping.Default,
        [typeof(char)] = CharTypeMapping.Default,
        [typeof(DateOnly)] = DateOnlyTypeMapping.Default,
        [typeof(DateTime)] = DateTimeTypeMapping.Default,
        [typeof(DateTimeOffset)] = DateTimeOffsetTypeMapping.Default,
        [typeof(decimal)] = DecimalTypeMapping.Default,
        [typeof(double)] = DoubleTypeMapping.Default,
        [typeof(float)] = FloatTypeMapping.Default,
        [typeof(Guid)] = GuidTypeMapping.Default,
        [typeof(int)] = IntTypeMapping.Default,
        [typeof(long)] = LongTypeMapping.Default,
        [typeof(sbyte)] = SByteTypeMapping.Default,
        [typeof(short)] = ShortTypeMapping.Default,
        [typeof(string)] = StringTypeMapping.Default,
        [typeof(TimeOnly)] = TimeOnlyTypeMapping.Default,
        [typeof(TimeSpan)] = TimeSpanTypeMapping.Default,
        [typeof(uint)] = UIntTypeMapping.Default,
        [typeof(ulong)] = ULongTypeMapping.Default,
        [typeof(ushort)] = UShortTypeMapping.Default,
    };

    /// <summary>
    ///     The instance a compiled model clones every other mapping from.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Required by <c>CSharpRuntimeAnnotationCodeGenerator.CreateDefaultTypeMapping</c>,
    ///         which emits <c>InfoCarrierTypeMapping.Default.Clone(…)</c> rather than a
    ///         constructor call and refuses outright — <i>"the mapping type must have a
    ///         'public static readonly …Default' property"</i> — when there is none. It is looked
    ///         up by <c>GetProperty("Default")</c>, so a field will not do; EF's own
    ///         <c>InMemoryTypeMapping</c> declares exactly this line.
    ///     </para>
    ///     <para>
    ///         <c>typeof(object)</c> because it is never used as a mapping: it is the receiver of
    ///         a <c>Clone</c> that replaces the CLR type, and the generator emits an explicit
    ///         <c>clrType:</c> argument for every mapping whose type differs from this one's.
    ///     </para>
    /// </remarks>
    public static InfoCarrierTypeMapping Default { get; } = new(typeof(object));

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierTypeMapping" /> class.
    /// </summary>
    /// <param name="clrType">The CLR type being mapped.</param>
    /// <param name="comparer">
    ///     How EF compares and snapshots a value of this type. Left null for the CLR primitives,
    ///     whose default comparer is correct; supplied for a type EF cannot compare structurally
    ///     on its own — a NetTopologySuite geometry, whose comparer is built by reflection so that
    ///     naming it costs no package reference.
    /// </param>
    /// <param name="keyComparer">As <paramref name="comparer" />, for a value used as a key.</param>
    /// <param name="jsonValueReaderWriter">
    ///     How EF reads and writes a value of this type as JSON. Not optional in practice: a
    ///     <em>primitive collection</em> — <c>List&lt;string&gt;</c> on a complex type, say — is
    ///     mappable only when its element has one, and without it the property is left unmapped
    ///     and a constructor that takes it fails to bind at model-building time. EF's own
    ///     <c>InMemoryTypeMappingSource</c> supplies it for the same reason.
    /// </param>
    public InfoCarrierTypeMapping(
        Type clrType,
        Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer? comparer = null,
        Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer? keyComparer = null,
        Microsoft.EntityFrameworkCore.Storage.Json.JsonValueReaderWriter? jsonValueReaderWriter = null)
        : base(
            new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    clrType,
                    converter: null,
                    comparer,
                    keyComparer,
                    jsonValueReaderWriter: jsonValueReaderWriter),
                StoreTypeNameFor(clrType)))
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierTypeMapping" /> class from
    ///     cloned parameters.
    /// </summary>
    /// <param name="parameters">The parameters to clone from.</param>
    protected InfoCarrierTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    /// <inheritdoc />
    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new InfoCarrierTypeMapping(parameters);

    /// <inheritdoc />
    /// <remarks>
    ///     Delegates to EF's own neutral mapping so that the text is EF's and not this
    ///     provider's. The generic base would format through <c>{0}</c>, which writes
    ///     <c>True</c> for a <see cref="bool" /> and is valid SQL almost nowhere; EF's
    ///     <c>BoolTypeMapping</c> writes <c>1</c>, which every store this provider can sit in
    ///     front of accepts.
    /// </remarks>
    protected override string GenerateNonNullSqlLiteral(object value)
    {
        RelationalTypeMapping? neutral = NeutralFor(ClrType);

        if (neutral is null)
        {
            return base.GenerateNonNullSqlLiteral(value);
        }

        // An enum is written as its underlying number, which is what every relational provider
        // does and what the generic format would get wrong by writing the member's name.
        Type stored = Underlying(ClrType);

        return neutral.GenerateSqlLiteral(
            stored == ClrType || value is null
                ? value
                : Convert.ChangeType(value, stored, CultureInfo.InvariantCulture));
    }

    // EF's own neutral mapping for a CLR type, or null when EF ships none. These live in
    // `EFCore.Relational` rather than in any provider, so their store type names and their
    // literal syntax are the shape every relational provider starts from and belong to no
    // particular database. That is exactly what a shared model may carry.
    private static RelationalTypeMapping? NeutralFor(Type clrType)
        => Neutral.TryGetValue(Underlying(clrType), out RelationalTypeMapping? mapping)
            ? mapping
            : null;

    private static Type Underlying(Type clrType)
    {
        Type type = Nullable.GetUnderlyingType(clrType) ?? clrType;

        return type.IsEnum ? Enum.GetUnderlyingType(type) : type;
    }

    // The store type name, taken from EF's neutral mapping where there is one. It is still a
    // fiction — the client has no store — but it is EF's fiction rather than one invented here,
    // and it names no database.
    private static string StoreTypeNameFor(Type clrType)
        => NeutralFor(clrType)?.StoreType ?? Underlying(clrType).Name;
}
