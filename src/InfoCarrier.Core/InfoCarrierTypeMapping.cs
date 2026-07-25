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
    public InfoCarrierTypeMapping(Type clrType)
        : base(new CoreTypeMappingParameters(clrType))
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
