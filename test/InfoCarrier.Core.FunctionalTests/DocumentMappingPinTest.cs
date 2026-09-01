// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Reflection;
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

    // `InfoCarrierHierarchyMappingConvention`'s four. The three strategy VALUES are pinned as well
    // as the annotation name, because they are compared against rather than merely read: a rename
    // of `"TPT"` would leave this client keeping a discriminator the server had dropped, which is
    // silent wrong data rather than an error.
    [ConditionalFact]
    public void The_mapping_strategy_annotation_and_values_are_still_EFs()
    {
        Assert.Equal(
            RelationalAnnotationNames.MappingStrategy,
            InfoCarrierHierarchyMappingConvention.MappingStrategyAnnotation);
        Assert.Equal(
            RelationalAnnotationNames.TphMappingStrategy,
            InfoCarrierHierarchyMappingConvention.TphMappingStrategy);
        Assert.Equal(
            RelationalAnnotationNames.TptMappingStrategy,
            InfoCarrierHierarchyMappingConvention.TptMappingStrategy);
        Assert.Equal(
            RelationalAnnotationNames.TpcMappingStrategy,
            InfoCarrierHierarchyMappingConvention.TpcMappingStrategy);
    }

    // `InfoCarrierEvaluatableExpressionFilter`'s one, and it is a TYPE name rather than an
    // annotation name -- but it is named by string for exactly the same reason and fails the same
    // silent way. A rename would stop the filter matching, `EF.Functions.Collate` over a constant
    // operand would go back to being evaluated on the client, and the only symptom is EF's own
    // "switched to client-evaluation" from a query that used to work.
    [ConditionalFact]
    public void The_relational_DbFunctions_host_name_is_still_EFs()
        => Assert.Equal(
            InfoCarrierEvaluatableExpressionFilter.RelationalDbFunctionsExtensionsName,
            typeof(RelationalDbFunctionsExtensions).FullName);

    // `ModelDbFunctions`, and this one is pinned TWICE because it names two things by string: the
    // annotation, and the `MethodInfo` property on the value behind it. The second is read by
    // reflection, so a rename would not even fail to compile -- it would answer "this model maps no
    // functions", which puts every mapped function back to being refused at the client boundary and
    // client-evaluated into its own `throw`. That is 81 tests, silently.
    [ConditionalFact]
    public void The_db_functions_annotation_name_is_still_EFs()
        => Assert.Equal(
            ModelDbFunctions.DbFunctionsAnnotation,
            RelationalAnnotationNames.DbFunctions);

    [ConditionalFact]
    public void The_db_function_methods_agree_with_EFs_own_GetDbFunctions()
    {
        using var context = new DbFunctionPinContext();
        IModel model = context.Model;

        // EF's own answer, through the API this provider cannot call.
        List<MethodInfo> expected = model.GetDbFunctions()
            .Select(f => f.MethodInfo)
            .OfType<MethodInfo>()
            .OrderBy(m => m.Name)
            .ToList();

        List<MethodInfo> actual = ModelDbFunctions.ForModel(model)
            .OrderBy(m => m.Name)
            .ToList();

        // Asserted rather than hoped for: a model with no functions would satisfy the equality
        // below while proving nothing, and that is exactly the failure mode a rename produces.
        Assert.Equal(2, expected.Count);
        Assert.Equal(expected, actual);

        // And the empty case, which is every other fixture in this suite.
        using var plain = new PinContext();
        Assert.Empty(ModelDbFunctions.ForModel(plain.Model));
    }

    // `InfoCarrierQueryFilterRewritingConvention`'s two. The second is derived from EF rather than
    // written down: the methods the convention must leave alone are exactly those on this class
    // whose FIRST parameter is a `DbSet<>`, because that is the parameter core EF's rewriter fills
    // with an `IQueryable`. A new overload group EF adds would fail this test rather than fail a
    // caller's model build with `ArgumentException`.
    [ConditionalFact]
    public void The_FromSql_host_name_is_still_EFs()
        => Assert.Equal(
            InfoCarrierQueryFilterRewritingConvention.FromSqlDeclaringTypeName,
            typeof(RelationalQueryableExtensions).FullName);

    [ConditionalFact]
    public void Every_DbSet_taking_method_EF_declares_there_is_one_the_convention_leaves_alone()
    {
        string[] takingADbSet = typeof(RelationalQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetParameters() is [{ ParameterType: { IsGenericType: true } first }, ..]
                && first.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(m => m.Name)
            .Distinct()
            .Order()
            .ToArray();

        // Asserted rather than hoped for, as above: an empty set would satisfy the equality below
        // while proving nothing.
        Assert.NotEmpty(takingADbSet);
        Assert.Equal(
            InfoCarrierQueryFilterRewritingConvention.FromSqlMethodNames.Order().ToArray(),
            takingADbSet);
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

    private class DbFunctionPinContext : DbContext
    {
        public static int Doubled(int value)
            => throw new NotSupportedException();

        public static string Tagged(string value)
            => throw new NotSupportedException();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFunctionPinEntity>();
            modelBuilder.HasDbFunction(typeof(DbFunctionPinContext).GetMethod(nameof(Doubled))!);
            modelBuilder.HasDbFunction(typeof(DbFunctionPinContext).GetMethod(nameof(Tagged))!);
        }
    }

    private class DbFunctionPinEntity
    {
        public int Id { get; set; }
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
