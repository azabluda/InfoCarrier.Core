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
        if (clrType is null)
        {
            return base.FindMapping(mappingInfo);
        }

        if (clrType.IsValueType
            || clrType == typeof(string)
            || (clrType == typeof(byte[]) && mappingInfo.ElementTypeMapping == null))
        {
            return new InfoCarrierTypeMapping(
                clrType,
                jsonValueReaderWriter: Dependencies.JsonValueReaderWriterSource.FindReaderWriter(clrType));
        }

        // A NetTopologySuite geometry is the one reference type that has to be mapped as a
        // scalar. Without this the property falls through, the mapping source says "not a
        // scalar", and EF's convention concludes the only other thing available — that
        // `Point` is an *entity type*, which fails model validation. `GeometryValueComparer<>`
        // lives in EFCore proper and is built by reflection, so naming NetTopologySuite here
        // costs no package reference (mirrors `InMemoryTypeMappingSource`).
        if (IsGeometry(clrType))
        {
            var comparer = (Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer)Activator.CreateInstance(
                typeof(Microsoft.EntityFrameworkCore.ChangeTracking.GeometryValueComparer<>).MakeGenericType(clrType))!;

            return new InfoCarrierTypeMapping(
                clrType,
                comparer,
                comparer,
                Dependencies.JsonValueReaderWriterSource.FindReaderWriter(clrType));
        }

        return base.FindMapping(mappingInfo);
    }

    private static bool IsGeometry(Type clrType)
    {
        for (Type? t = clrType; t is not null; t = t.BaseType)
        {
            if (t.FullName == "NetTopologySuite.Geometries.Geometry")
            {
                return true;
            }
        }

        return false;
    }
}
