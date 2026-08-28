// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core;

/// <summary>
///     The client's model validator: EF's core one, with the discriminator requirement lifted for
///     an inheritance hierarchy that does not declare one.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this exists.</b> <see cref="ModelValidator.ValidateDiscriminatorValues(IEntityType)" />
///         throws <c>CoreStrings.NoDiscriminatorProperty</c> for any hierarchy with derived types
///         and no discriminator. That rule belongs to table-per-hierarchy, and every provider that
///         can map inheritance another way lifts it: <c>RelationalModelValidator</c> overrides the
///         same method and takes an <c>else</c> branch for the discriminator-less case, which is
///         what makes TPT and TPC legal there.
///     </para>
///     <para>
///         <b>Why absence of a discriminator is the right signal, and the mapping strategy is
///         not.</b> A TPT model does not have to carry <c>Relational:MappingStrategy</c>. EF's own
///         <c>TPTInheritanceQueryFixture</c> never sets it — it calls <c>ToTable</c> once per type
///         and ignores the discriminator property — so a check for that annotation would never
///         fire. <c>RelationalModelValidator</c> does not read it for this decision either.
///     </para>
///     <para>
///         <b>Why it is sound here.</b> This client maps to no store, so it has no stake in how the
///         server's provider lays a hierarchy out; that provider validates its own model. What the
///         client needs is to name the concrete type of a materialized entity, and the wire carries
///         that by name in <see cref="Expressions.TypeNode.EntityTypeName" /> rather than by
///         reading a discriminator column. A model that really is TPH still gets its discriminator
///         from convention and is still validated below.
///     </para>
/// </remarks>
public class InfoCarrierModelValidator(ModelValidatorDependencies dependencies)
    : ModelValidator(dependencies)
{
    /// <inheritdoc />
    protected override void ValidateInheritanceMapping(
        IModel model,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        // `ModelExtensions.GetRootEntityTypes` is what the base walks, and it is internal for the
        // one line it is. Spelled out here rather than reached for behind an EF1001 pragma.
        foreach (IEntityType rootEntityType in model.GetEntityTypes().Where(e => e.BaseType is null))
        {
            if (rootEntityType.FindDiscriminatorProperty() is not null)
            {
                ValidateDiscriminatorValues(rootEntityType);
                continue;
            }

            // No discriminator: the server's provider owns the inheritance mapping. Nested complex
            // types are still validated, exactly as the base does on the path it returns early on.
            foreach (IEntityType derivedType in rootEntityType.GetDerivedTypesInclusive())
            {
                foreach (IComplexProperty complexProperty in derivedType.GetDeclaredComplexProperties())
                {
                    ValidateDiscriminatorValues(complexProperty.ComplexType);
                }
            }
        }
    }
}
