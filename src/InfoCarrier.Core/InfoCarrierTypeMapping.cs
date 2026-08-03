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
    ///     Initializes a new instance of the <see cref="InfoCarrierTypeMapping" /> class.
    /// </summary>
    /// <param name="clrType">The CLR type being mapped.</param>
    /// <param name="jsonValueReaderWriter">
    ///     How EF reads and writes a value of this type as JSON. Not optional in practice: a
    ///     <em>primitive collection</em> — <c>List&lt;string&gt;</c> on a complex type, say — is
    ///     mappable only when its element has one, and without it the property is left unmapped
    ///     and a constructor that takes it fails to bind at model-building time. EF's own
    ///     <c>InMemoryTypeMappingSource</c> supplies it for the same reason.
    /// </param>
    public InfoCarrierTypeMapping(
        Type clrType, Microsoft.EntityFrameworkCore.Storage.Json.JsonValueReaderWriter? jsonValueReaderWriter = null)
        : base(new CoreTypeMappingParameters(clrType, jsonValueReaderWriter: jsonValueReaderWriter))
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
