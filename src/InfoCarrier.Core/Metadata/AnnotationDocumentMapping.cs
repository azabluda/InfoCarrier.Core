// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;

// Internal EF Core API usage. This provider is built on EF Core internals by design (CLAUDE.md),
// and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.Metadata;

/// <summary>
///     The default <see cref="IInfoCarrierDocumentMapping" />: reads the container annotation EF's
///     relational providers write.
/// </summary>
/// <remarks>
///     <para>
///         <b>The two names below are EF's own constants, and the paragraph that used to stand
///         here argued for the opposite (corrected 2026-09-09).</b> It said naming the constant
///         "would drag the relational package back into a provider whose client is never a
///         relational context", so the names were spelled as strings and pinned by a test. R133
///         reversed that once the reference came back: an EF rename is a build error now, and the
///         268-line pin test is deleted.
///     </para>
///     <para>
///         <b>Why the seam stays, asked and answered 2026-09-09.</b> With the reference back, this
///         class could call <c>GetContainerColumnName()</c> directly and the interface could go.
///         It does not go, for two reasons. It is <em>published API</em>: both types shipped in
///         <c>10.0.0</c> and <see cref="InfoCarrierDatabase" />'s public constructor takes the
///         interface, so removing any of it is a binary break package validation refuses. And the
///         question is still store-shaped, which is the whole argument in
///         <see cref="IInfoCarrierDocumentMapping" />'s own remarks: a document store answers it
///         by the property's shape rather than by this annotation, and that is the seam's reason
///         for existing rather than an artefact of the missing reference.
///     </para>
///     <para>
///         <b>The walk is EF's, reproduced rather than approximated.</b>
///         <c>RelationalTypeBaseExtensions.GetContainerColumnName()</c> falls back through the
///         ownership chain for an entity type and through the declaring type for a complex type,
///         because a nested owned type inherits the container from whichever ancestor declared it.
///         Reading the annotation on the type alone would answer <see langword="null" /> for every
///         nested type and reintroduce B12 one level down.
///     </para>
/// </remarks>
public sealed class AnnotationDocumentMapping : IInfoCarrierDocumentMapping
{
    /// <summary>
    ///     <c>RelationalAnnotationNames.ContainerColumnName</c>. EF's own constant, so a rename is a
    ///     build error.
    /// </summary>
    public const string ContainerColumnNameAnnotation = RelationalAnnotationNames.ContainerColumnName;

    /// <summary>
    ///     <c>RelationalKeyDiscoveryConvention.SynthesizedOrdinalPropertyName</c>. EF's own constant, so a rename is a
    ///     build error.
    /// </summary>
    public const string SynthesizedOrdinal =
        Microsoft.EntityFrameworkCore.Metadata.Conventions.RelationalKeyDiscoveryConvention
            .SynthesizedOrdinalPropertyName;

    /// <inheritdoc />
    public IEnumerable<string> ContainerAnnotationNames { get; } = [ContainerColumnNameAnnotation];

    /// <inheritdoc />
    public string SynthesizedOrdinalPropertyName => SynthesizedOrdinal;

    /// <inheritdoc />
    public string? FindContainerName(IReadOnlyTypeBase type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.FindAnnotation(ContainerColumnNameAnnotation) is { } annotation)
        {
            return (string?)annotation.Value;
        }

        return type switch
        {
            IReadOnlyEntityType entityType
                => entityType.FindOwnership()?.PrincipalEntityType is { } owner
                    ? FindContainerName(owner)
                    : null,
            IReadOnlyComplexType complexType
                => FindContainerName(complexType.ComplexProperty.DeclaringType),
            _ => null,
        };
    }
}
