// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace InfoCarrier.Core;

/// <summary>
///     Removes the discriminator from a hierarchy the backing store maps without one, so that the
///     client's model reaches the same answer the server's does.
/// </summary>
/// <remarks>
///     <para>
///         <b>The defect this closes.</b> Core EF's <c>DiscriminatorConvention</c> gives every
///         hierarchy a discriminator, and the convention that takes it away again for TPT and TPC
///         — <c>EntityTypeHierarchyMappingConvention</c> — ships in
///         <c>Microsoft.EntityFrameworkCore.Relational</c>, which this client does not have. So a
///         TPT model built here kept a discriminator the server's model had dropped: a fact
///         computed twice by two providers and disagreeing, which is the failure mode
///         <c>CLAUDE.md</c> names.
///     </para>
///     <para>
///         <b>Reading the annotations by name is deliberate, and it is not new.</b>
///         <see cref="Metadata.AnnotationDocumentMapping" /> already reads
///         <c>Relational:ContainerColumnName</c> this way rather than reference the relational
///         package (M9, J5). The strings below are pinned against EF's own constants by
///         <c>DocumentMappingPinTest</c>, in the test project, which is where the relational
///         reference belongs.
///     </para>
///     <para>
///         <b>Why it is narrower than EF's version, on purpose.</b> EF compares
///         <c>GetTableName()</c>, which falls back through a convention to the <c>DbSet</c> name,
///         so a derived type with no <c>ToTable</c> of its own still answers with its root's table.
///         This client has no such fallback, so comparing raw annotations would read "root names a
///         table, derived names none" as a difference and strip the discriminator from a plain TPH
///         model. The test here is therefore that the DERIVED type names a store object of its own
///         which differs from the nearest one an ancestor names — the case EF's comparison actually
///         detects.
///     </para>
/// </remarks>
public class InfoCarrierHierarchyMappingConvention : IModelFinalizingConvention
{
    /// <summary>
    ///     <c>RelationalAnnotationNames.MappingStrategy</c>. Pinned by <c>DocumentMappingPinTest</c>.
    /// </summary>
    public const string MappingStrategyAnnotation = "Relational:MappingStrategy";

    /// <summary>
    ///     <c>RelationalAnnotationNames.TptMappingStrategy</c>. Pinned by <c>DocumentMappingPinTest</c>.
    /// </summary>
    public const string TptMappingStrategy = "TPT";

    /// <summary>
    ///     <c>RelationalAnnotationNames.TpcMappingStrategy</c>. Pinned by <c>DocumentMappingPinTest</c>.
    /// </summary>
    public const string TpcMappingStrategy = "TPC";

    /// <summary>
    ///     <c>RelationalAnnotationNames.TphMappingStrategy</c>. Pinned by <c>DocumentMappingPinTest</c>.
    /// </summary>
    public const string TphMappingStrategy = "TPH";

    private const string TableNameAnnotation = "Relational:TableName";
    private const string SchemaAnnotation = "Relational:Schema";
    private const string ViewNameAnnotation = "Relational:ViewName";
    private const string ViewSchemaAnnotation = "Relational:ViewSchema";

    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        HashSet<IConventionEntityType> nonTphRoots = [];

        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.BaseType is null)
            {
                continue;
            }

            IConventionEntityType root = entityType.GetRootType();

            // The strategy is read off the type first and its root second, as EF does: a derived
            // type may not restate it, and the root is where `UseTptMappingStrategy` puts it.
            string? strategy = (string?)(entityType[MappingStrategyAnnotation] ?? root[MappingStrategyAnnotation]);

            if (strategy is TptMappingStrategy or TpcMappingStrategy)
            {
                nonTphRoots.Add(root);
            }
            else if (strategy is not TphMappingStrategy
                     && (NamesItsOwn(entityType, TableNameAnnotation, SchemaAnnotation)
                         || NamesItsOwn(entityType, ViewNameAnnotation, ViewSchemaAnnotation)))
            {
                // No strategy was stated, so the mapping is inferred from the store objects, which
                // is how `ToTable` per type expresses TPT.
                nonTphRoots.Add(root);
            }
        }

        foreach (IConventionEntityType root in nonTphRoots)
        {
            root.Builder.HasNoDiscriminator();
        }
    }

    private static bool NamesItsOwn(IConventionEntityType entityType, string nameAnnotation, string schemaAnnotation)
    {
        if (entityType[nameAnnotation] is not string name)
        {
            return false;
        }

        for (IConventionEntityType? ancestor = entityType.BaseType; ancestor is not null; ancestor = ancestor.BaseType)
        {
            if (ancestor[nameAnnotation] is string ancestorName)
            {
                return ancestorName != name
                    || (string?)ancestor[schemaAnnotation] != (string?)entityType[schemaAnnotation];
            }
        }

        // No ancestor names one at all, so this derived type names a store object its base does
        // not share. That is the shape EF reads as TPT.
        return true;
    }
}
