// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     A concrete <see cref="CoreTypeMapping" /> for client-side CLR primitives. No store
///     conversion — the server owns the real store mapping.
/// </summary>
public class InfoCarrierTypeMapping : CoreTypeMapping
{
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
            new CoreTypeMappingParameters(
                clrType,
                converter: null,
                comparer,
                keyComparer,
                jsonValueReaderWriter: jsonValueReaderWriter))
    {
    }

    private InfoCarrierTypeMapping(CoreTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    /// <inheritdoc />
    protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
        => new InfoCarrierTypeMapping(parameters);

    /// <inheritdoc />
    public override CoreTypeMapping WithComposedConverter(
        Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter? converter,
        Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer? comparer,
        Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer? providerComparer,
        CoreTypeMapping? elementMapping,
        Microsoft.EntityFrameworkCore.Storage.Json.JsonValueReaderWriter? jsonValueReaderWriter)
        => new InfoCarrierTypeMapping(
            Parameters.WithComposedConverter(converter, comparer, providerComparer, elementMapping, jsonValueReaderWriter));
}
