// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side type mapping source. InfoCarrier remotes queries, so type mapping is only
///     needed for model building and change tracking on the client — it uses the standard CLR
///     type mappings, not a store-specific mapping.
/// </summary>
public class InfoCarrierTypeMappingSource : TypeMappingSource
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierTypeMappingSource" /> class.
    /// </summary>
    public InfoCarrierTypeMappingSource(TypeMappingSourceDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <inheritdoc />
    protected override CoreTypeMapping? FindMapping(in TypeMappingInfo mappingInfo)
    {
        // Map CLR primitives (value types, string, byte[]) so the client model builds and
        // change tracking works. No store-specific conversion — the server owns the real
        // store mapping.
        Type? clrType = mappingInfo.ClrType;
        if (clrType is not null
            && (clrType.IsValueType
                || clrType == typeof(string)
                || (clrType == typeof(byte[]) && mappingInfo.ElementTypeMapping == null)))
        {
            return new InfoCarrierTypeMapping(clrType);
        }

        return base.FindMapping(mappingInfo);
    }
}
