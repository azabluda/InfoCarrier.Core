// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side type mapping source. InfoCarrier remotes queries, so type mapping is only
///     needed for model building and change tracking on the client — it uses the standard CLR
///     type mappings, not a store-specific mapping.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="InfoCarrierTypeMappingSource" /> class.
/// </remarks>
public class InfoCarrierTypeMappingSource(TypeMappingSourceDependencies dependencies) : TypeMappingSource(dependencies)
{

    /// <inheritdoc />
    protected override CoreTypeMapping? FindMapping(in TypeMappingInfo mappingInfo)
    {
        // Map CLR primitives (scalar value types, string, byte[]) so the client model builds
        // and change tracking works. No store-specific conversion — the server owns the real
        // store mapping.
        Type? clrType = mappingInfo.ClrType;
        if (clrType is null)
        {
            return base.FindMapping(mappingInfo);
        }

        JsonValueReaderWriter? jsonValueReaderWriter =
            Dependencies.JsonValueReaderWriterSource.FindReaderWriter(clrType);

        if (clrType == typeof(string)
            || (clrType == typeof(byte[]) && mappingInfo.ElementTypeMapping == null))
        {
            return new InfoCarrierTypeMapping(clrType, jsonValueReaderWriter: jsonValueReaderWriter);
        }

        // A value type is a *scalar* only when EF recognises it as one — every BCL primitive,
        // `Guid` / `DateTime` / `decimal` / …, and every enum, has a JSON reader/writer; a plain
        // `struct` (a complex-type candidate) does not. `IsValueType` alone was too broad: it
        // claimed the struct complex types too, so `PropertyDiscoveryConvention` mapped them as
        // primitives before `ComplexPropertyDiscoveryConvention` could see them, and the client
        // model then lost every nested complex property whose CLR type is a struct
        // (`Culture.License`, `Manufacturer.Tog`, …) while the server's model kept them. On that
        // divergence `EntityFinder.BuildProjection` — which `GetDatabaseValues()` and `Reload()`
        // run against the *client* model — emitted `EF.Property<TStruct>(…)` for a value the
        // server can only read as a complex type, and the server client-evaluated the projection
        // into `EF.Property` at materialisation: "may only be used within EF LINQ queries" (#69).
        // A converted struct key is unaffected: `KeysWithConvertersTestBase` configures every one
        // with an explicit `Property(e => e.Id).HasConversion(...)`, so it never depends on this
        // source to be classified as a property.
        if (clrType.IsValueType
            && (jsonValueReaderWriter is not null
                || Dependencies.JsonValueReaderWriterSource.FindReaderWriter(
                    Nullable.GetUnderlyingType(clrType) ?? clrType) is not null))
        {
            return new InfoCarrierTypeMapping(clrType, jsonValueReaderWriter: jsonValueReaderWriter);
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
