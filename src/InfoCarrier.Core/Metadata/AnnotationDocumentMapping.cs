// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Metadata;

/// <summary>
///     The default <see cref="IInfoCarrierDocumentMapping" />: reads the container annotation EF's
///     relational providers write, <b>by its string name</b>, so that this package needs no
///     reference to <c>Microsoft.EntityFrameworkCore.Relational</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>The string is the whole cost of M9's J5, and it was accepted deliberately.</b>
///         Naming the constant would be type-safe and would drag the relational package back into
///         a provider whose client is never a relational context. Naming the string means an EF
///         rename becomes a silent behaviour change rather than a build error — so the strings
///         below are pinned by a test that asserts them equal to EF's own constants, and the test
///         project is where the relational reference belongs.
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
