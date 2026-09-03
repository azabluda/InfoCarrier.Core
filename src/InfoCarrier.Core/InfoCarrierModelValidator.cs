// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

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
/// <param name="dependencies">EF's model-validator dependencies.</param>
/// <param name="relationalQueryRoots">
///     The level-1 seam (#97). Whatever is registered here is the only thing on a client that says
///     whether the backing store is relational, which is why the half-configuration check below
///     reads it. <c>NoRelationalQueryRoots</c> means nothing has said so.
/// </param>
public class InfoCarrierModelValidator(
    ModelValidatorDependencies dependencies,
    Metadata.IInfoCarrierRelationalQueryRoots relationalQueryRoots)
    : ModelValidator(dependencies)
{
    /// <summary>
    ///     The model annotation <c>InfoCarrier.Core.Relational</c> stamps when its conventions have
    ///     run. Absence of it, on a client that has said its store IS relational, is the
    ///     half-configuration this validator refuses.
    /// </summary>
    public const string RelationalConventionsAnnotation = "InfoCarrier:RelationalConventions";

    /// <summary>
    ///     <c>RelationalAnnotationNames.TableName</c>. EF's own constant, so a rename is a build error.
    /// </summary>
    public const string TableNameAnnotation = RelationalAnnotationNames.TableName;

    /// <summary>
    ///     <c>RelationalAnnotationNames.ViewName</c>. EF's own constant, so a rename is a build error.
    /// </summary>
    public const string ViewNameAnnotation = RelationalAnnotationNames.ViewName;

    /// <summary>
    ///     <c>RelationalAnnotationNames.MappingStrategy</c>. EF's own constant, so a rename is a build error.
    /// </summary>
    public const string MappingStrategyAnnotation = RelationalAnnotationNames.MappingStrategy;

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         <b>THE HALF-CONFIGURED CLIENT, refused here rather than left to produce a model that
    ///         quietly disagrees with the server's (#97 level 2).</b> Since R128 the relational
    ///         conventions live in <c>InfoCarrier.Core.Relational</c>, so a client whose store is
    ///         relational and which does not register that package builds a model EF's own
    ///         relational conventions never touched — a TPT hierarchy keeps the discriminator the
    ///         server's model dropped, and `FromSqlRaw` on a non-TPH root is admitted where EF
    ///         refuses it. Neither raises anything by itself. This does.
    ///     </para>
    ///     <para>
    ///         <b>The trigger is a configuration mismatch and NOT the shape of the model, and that
    ///         is the only sound trigger available.</b> A model that calls <c>ToTable</c> is
    ///         perfectly ordinary on a client whose store is not relational — ADR-009 Tier A builds
    ///         many — so the model cannot be read as evidence. What can be read is the client's own
    ///         statement: something registered <c>AddInfoCarrierRelational()</c>, so the store IS
    ///         relational, and the conventions that go with it did not run.
    ///     </para>
    ///     <para>
    ///         <b>IT WARNS AND DOES NOT THROW, and two measured false positives are why.</b> The
    ///         first version threw on the configuration alone and broke
    ///         <c>OptimisticConcurrencyTestBase.External_model_builder_uses_validation</c>, which
    ///         hands over a model built externally with <c>UseModel</c> — a legitimate pattern no
    ///         convention set ever stamps. Narrowing the trigger to configuration <em>and</em>
    ///         symptom fixed that one and left another: <c>F1FixtureBase</c> also builds its model
    ///         externally, through this provider's own conventions, so a relational client running
    ///         it legitimately has no relational stamp. **A diagnostic that refuses a legitimate
    ///         model is worse than the silence it replaces**, so this is a warning that names the
    ///         remedy, and the remedy is the part that matters.
    ///     </para>
    ///     <para>
    ///         <b>Two things it deliberately does NOT catch, both stated so nobody reads silence as
    ///         coverage.</b> A client that says nothing at all cannot be checked — nothing on it
    ///         knows the server is relational, and finding out needs the model handshake §6a D2
    ///         describes and this repository has not built. And the options-carried route
    ///         (<c>UseRelationalQueryRoots</c>) is invisible here, because <c>IModelValidator</c> is
    ///         a singleton and that value is per context.
    ///     </para>
    /// </remarks>
    public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (relationalQueryRoots is not Metadata.NoRelationalQueryRoots
            && model.FindAnnotation(RelationalConventionsAnnotation) is null
            && HasUnmappedNonTphHierarchy(model))
        {
            logger.Logger.LogWarning("{Message}", HalfConfiguredMessage);
        }

        base.Validate(model, logger);
    }

    /// <summary>
    ///     What a half-configured client is told. Public so a test can assert the text rather than
    ///     a substring of it.
    /// </summary>
    public const string HalfConfiguredMessage =
        "This client says its backing store is relational -- something registered "
        + "AddInfoCarrierRelational() -- but the relational model conventions did not run, so its "
        + "model is missing what EF's own relational conventions would have supplied. A hierarchy "
        + "mapped with TPT or TPC keeps the discriminator the server's model does not have, and the "
        + "two models then disagree silently. Call AddInfoCarrierRelationalClient() from the "
        + "InfoCarrier.Core.Relational package on the client's services instead of "
        + "AddInfoCarrierRelational(); it registers both halves.";

    /// <summary>
    ///     Whether the model shows the symptom the missing conventions leave behind: a hierarchy
    ///     mapped to more than one store object that <em>still</em> carries a discriminator.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The configuration alone is not enough to refuse on, and a false positive taught
    ///         that.</b> The first version of this guard threw whenever the client had said its
    ///         store was relational and the stamp was absent. It broke
    ///         <c>OptimisticConcurrencyTestBase.External_model_builder_uses_validation</c>, which
    ///         builds a model externally and hands it over with <c>UseModel</c> — a legitimate
    ///         pattern that no convention set ever touches, so no stamp can reach it. Refusing that
    ///         replaced EF's own <c>EntityRequiresKey</c> message with this one.
    ///     </para>
    ///     <para>
    ///         <b>So the trigger is configuration AND symptom, and both are needed.</b> The symptom
    ///         is precisely what <c>EntityTypeHierarchyMappingConvention</c> would have removed: a
    ///         derived type naming a store object of its own, or an explicit TPT/TPC mapping
    ///         strategy, while the root still has the discriminator core EF gives every hierarchy.
    ///         A model with no inheritance cannot show it, which is why the external-model test
    ///         passes again.
    ///     </para>
    ///     <para>
    ///         <b>Three strings, and they are a DETECTOR rather than a fixer.</b> R128 deleted the
    ///         four this package used to spell for the fixing convention. These three come back for
    ///         a different job, and the stakes are different with them: a string that stops matching
    ///         costs a missed diagnostic, never wrong data. They are pinned against EF's constants
    ///         by the compiler, because they are EF's own constants.
    ///     </para>
    /// </remarks>
    private static bool HasUnmappedNonTphHierarchy(IModel model)
    {
        foreach (IEntityType entityType in model.GetEntityTypes())
        {
            if (entityType.BaseType is null
                || entityType.GetRootType().FindDiscriminatorProperty() is null)
            {
                continue;
            }

            if (entityType[MappingStrategyAnnotation] is not null
                || entityType[TableNameAnnotation] is not null
                || entityType[ViewNameAnnotation] is not null)
            {
                return true;
            }
        }

        return false;
    }

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
