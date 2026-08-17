// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Xunit;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     Pins <see cref="AnnotationDocumentMapping" /> to EF's relational metadata, which it names by
///     <b>string</b> so that <c>InfoCarrier.Core</c> needs no reference to
///     <c>Microsoft.EntityFrameworkCore.Relational</c> (M9 J5, D3 answer (c)).
/// </summary>
/// <remarks>
///     <para>
///         <b>This test is the price of that choice, and it is the whole price.</b> Naming a
///         constant makes an EF rename a build error; naming a string makes it a silent behaviour
///         change — and B12's symptom was wrong data with no exception, so silent is exactly what
///         must not happen here. The test project may reference the relational assembly (ADR-013),
///         so this is where the two can be compared.
///     </para>
///     <para>
///         <b>Not at a store tier.</b> It builds a model and never opens a connection; SQLite is
///         here only because <c>ToJson()</c> is a relational model-building API. Nothing is queried
///         and nothing is saved.
///     </para>
///     <para>
///         The behavioural assertion matters more than the two constants. A rename would break the
///         constants; a change to how EF <em>resolves</em> the container — the ownership-chain
///         fallback that `AnnotationDocumentMapping` reproduces — would break only the walk, and
///         only for nested types, which is precisely the case B12 was.
///     </para>
/// </remarks>
public class DocumentMappingPinTest
{
    [ConditionalFact]
    public void The_annotation_name_is_still_EFs()
        => Assert.Equal(
            RelationalAnnotationNames.ContainerColumnName,
            AnnotationDocumentMapping.ContainerColumnNameAnnotation);

    [ConditionalFact]
    public void The_synthesized_ordinal_name_is_still_EFs()
        => Assert.Equal(
            RelationalKeyDiscoveryConvention.SynthesizedOrdinalPropertyName,
            AnnotationDocumentMapping.SynthesizedOrdinal);

    // `InfoCarrierValueGenerationConvention`'s two, pinned here for the same reason and in the same
    // place: every relational annotation the product names by string rather than by constant.
    [ConditionalFact]
    public void The_default_value_annotation_names_are_still_EFs()
    {
        Assert.Equal(
            RelationalAnnotationNames.DefaultValue,
            InfoCarrierValueGenerationConvention.DefaultValueAnnotation);
        Assert.Equal(
            RelationalAnnotationNames.DefaultValueSql,
            InfoCarrierValueGenerationConvention.DefaultValueSqlAnnotation);
    }

    [ConditionalFact]
    public void The_walk_agrees_with_EF_for_every_type_including_nested_ones()
    {
        using var context = new PinContext();
        IModel model = context.Model;
        var mapping = new AnnotationDocumentMapping();

        // Every type, not only the one that carries `ToJson()`: a nested owned type inherits its
        // container from an ancestor, and reading the annotation on the type alone answers null
        // for it.
        var compared = 0;
        var inContainer = 0;

        foreach (IEntityType entityType in model.GetEntityTypes())
        {
            Assert.Equal(entityType.GetContainerColumnName(), mapping.FindContainerName(entityType));
            compared++;
            if (entityType.GetContainerColumnName() is not null)
            {
                inContainer++;
            }

            foreach (IComplexProperty complexProperty in entityType.GetComplexProperties())
            {
                Assert.Equal(
                    complexProperty.ComplexType.GetContainerColumnName(),
                    mapping.FindContainerName(complexProperty.ComplexType));
                compared++;
            }
        }

        // The assertions above would all hold vacuously against a model with no `ToJson()` at all,
        // and against one where the walk never had to recurse. Both are asserted rather than hoped
        // for: `PinOwner` is outside a container, `PinItem` carries the annotation, and `PinDetail`
        // is nested under it and has one only through the walk.
        Assert.True(compared >= 3, $"only {compared} types compared");
        Assert.Equal(2, inContainer);
        Assert.Null(mapping.FindContainerName(model.FindEntityType(typeof(PinOwner))!));
        Assert.NotNull(mapping.FindContainerName(model.FindEntityType(typeof(PinDetail))!));
    }

    private class PinOwner
    {
        public int Id { get; set; }

        public List<PinItem> Items { get; set; } = [];
    }

    private class PinItem
    {
        public string? Label { get; set; }

        public PinDetail? Detail { get; set; }
    }

    private class PinDetail
    {
        public string? Note { get; set; }
    }

    private class PinContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PinOwner>()
                .OwnsMany(
                    o => o.Items,
                    b =>
                    {
                        b.ToJson();
                        b.OwnsOne(i => i.Detail);
                    });
    }
}
