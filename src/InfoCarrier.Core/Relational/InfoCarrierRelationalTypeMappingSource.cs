// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;

namespace InfoCarrier.Core.Relational;

/// <summary>
///     Maps client CLR values for model building and change tracking without choosing a database dialect.
/// </summary>
/// <remarks>
///     The same registered instance supplies both core and relational type-mapping interfaces.
///     Before 2026-11-22, the scalar rules lived in the unregistered
///     InfoCarrierTypeMappingSource, retained for version 10 binary compatibility. Version 11
///     removes that obsolete host and keeps its rules here, in the active service.
/// </remarks>
/// <param name="dependencies">EF's core type-mapping dependencies.</param>
/// <param name="relationalDependencies">EF's relational type-mapping dependencies.</param>
public class InfoCarrierRelationalTypeMappingSource(
    TypeMappingSourceDependencies dependencies,
    RelationalTypeMappingSourceDependencies relationalDependencies)
    : RelationalTypeMappingSource(dependencies, relationalDependencies)
{
    /// <inheritdoc />
    protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
        => FindInfoCarrierMapping(mappingInfo.ClrType, mappingInfo.ElementTypeMapping, Dependencies)
            ?? base.FindMapping(mappingInfo);

    // Scalar classification depends on the CLR type and EF's neutral JSON readers.
    private static InfoCarrierTypeMapping? FindInfoCarrierMapping(
        Type? clrType,
        CoreTypeMapping? elementTypeMapping,
        TypeMappingSourceDependencies dependencies)
    {
        // Map CLR primitives (scalar value types, string, byte[]) so the client model builds
        // and change tracking works. No store-specific conversion — the server owns the real
        // store mapping.
        if (clrType is null)
        {
            return null;
        }

        JsonValueReaderWriter? jsonValueReaderWriter =
            dependencies.JsonValueReaderWriterSource.FindReaderWriter(clrType);

        if (clrType == typeof(string)
            || (clrType == typeof(byte[]) && elementTypeMapping == null))
        {
            return InfoCarrierTypeMapping.Create(clrType, jsonValueReaderWriter: jsonValueReaderWriter);
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
                || dependencies.JsonValueReaderWriterSource.FindReaderWriter(
                    Nullable.GetUnderlyingType(clrType) ?? clrType) is not null))
        {
            return InfoCarrierTypeMapping.Create(clrType, jsonValueReaderWriter: jsonValueReaderWriter);
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

            return InfoCarrierTypeMapping.Create(
                clrType,
                comparer,
                comparer,
                dependencies.JsonValueReaderWriterSource.FindReaderWriter(clrType));
        }

        return null;
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
