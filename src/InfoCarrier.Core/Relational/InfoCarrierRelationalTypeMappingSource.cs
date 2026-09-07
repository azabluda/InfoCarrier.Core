// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core.Relational;

/// <summary>
///     The client's type mapping source, in the shape EF's relational half insists on.
/// </summary>
/// <remarks>
///     <para>
///         <b>A second class rather than a changed base, because the other one is published.</b>
///         <see cref="InfoCarrierTypeMappingSource" /> shipped in <c>10.0.0</c> deriving from
///         <see cref="TypeMappingSource" />, and EF's
///         <see cref="RelationalTypeMappingSource" /> does not derive from it. Re-basing the
///         published class is a binary break the pack gate refuses, and rightly: a consumer who
///         subclassed it would stop compiling. So this is additive, and the mapping decision is
///         shared rather than copied.
///     </para>
///     <para>
///         <b>"Relational" is about EF's surface, not about a database.</b> Every mapping it
///         returns is an <see cref="InfoCarrierTypeMapping" />, whose store type name and literal
///         syntax come from EF's own neutral table and name no store. What the relational base
///         buys is that this source satisfies <c>IRelationalTypeMappingSource</c>, which EF's
///         relational model building casts it to, and which building a relational model on the
///         client requires.
///     </para>
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
        => InfoCarrierTypeMappingSource.FindInfoCarrierMapping(
            mappingInfo.ClrType, mappingInfo.ElementTypeMapping, Dependencies)
            ?? base.FindMapping(mappingInfo);
}
