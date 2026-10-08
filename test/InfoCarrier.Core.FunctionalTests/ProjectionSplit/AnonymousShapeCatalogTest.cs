// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

public class AnonymousShapeCatalogTest
{
    [Fact]
    public void Model_bound_catalogs_refuse_other_model_instances()
    {
        Microsoft.EntityFrameworkCore.Metadata.IModel first = new Microsoft.EntityFrameworkCore.ModelBuilder().FinalizeModel();
        Microsoft.EntityFrameworkCore.Metadata.IModel second = new Microsoft.EntityFrameworkCore.ModelBuilder().FinalizeModel();
        Type original = new { CatalogModelScope = 1 }.GetType();
        AnonymousShapeCatalog catalog = AnonymousShapeCatalog.Create(first, [original]);
        var correct = new TypeNodeResolver(first);
        correct.UseAnonymousShapeCatalog(catalog);
        Assert.NotNull(correct.Resolve(new TypeNodeMapper(first).ToTypeNode(original)));

        Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver(second).UseAnonymousShapeCatalog(catalog));
    }

    [Fact]
    public void Mutating_the_trusted_input_list_cannot_add_or_remove_catalog_membership()
    {
        Type approved = new { CatalogImmutable = 1 }.GetType();
        Type unapproved = new { CatalogNotAdded = 2 }.GetType();
        Type[] inputs = [approved];
        AnonymousShapeCatalog catalog = AnonymousShapeCatalog.Create(null, inputs);
        inputs[0] = unapproved;
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(catalog);

        Assert.NotNull(resolver.Resolve(new TypeNodeMapper().ToTypeNode(approved)));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNodeMapper().ToTypeNode(unapproved)));
    }

    [Fact]
    public void Shape_graphs_share_the_total_descriptor_node_budget()
    {
        var leaf = new TypeNode { Name = typeof(int).FullName! };
        TypeNode shape = new TypeNodeMapper().ToTypeNode(new { CatalogNodeBudget = 1 }.GetType()) with
        {
            ShapeMembers = Enumerable.Range(0, 32).Select(i => "Member" + i).ToArray(),
            GenericArguments = Enumerable.Repeat(leaf, 32).ToArray(),
        };
        TypeNode graph = shape with { GenericArguments = Enumerable.Repeat(shape, 32).ToArray() };

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(graph));
        Assert.Contains("node budget", refusal.Message);
    }

    [Fact]
    public void Registered_nested_objects_and_arrays_restore_the_original_client_types()
    {
        var original = new { CatalogNested = new[] { new { Id = 7 } }, Empty = new { } };
        var mapper = new TypeNodeMapper();
        var client = new DynamicValueMapper(null, mapper, new TypeNodeResolver());
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original.GetType()]));
        var server = new DynamicValueMapper(null, new TypeNodeMapper(), resolver);

        DynamicValueNode sent = client.ToDynamicValue(original, original.GetType());
        object generated = server.FromDynamicValue(sent)!;
        object restored = client.FromDynamicValue(server.ToDynamicValue(generated, generated.GetType()))!;

        Assert.Equal(original.GetType(), restored.GetType());
        Assert.Equal(7, restored.GetType().GetProperty("CatalogNested")!.GetValue(restored) is Array values
            ? values.GetValue(0)!.GetType().GetProperty("Id")!.GetValue(values.GetValue(0)) : null);
    }

    [Fact]
    public void Another_catalog_cannot_reuse_an_unregistered_globally_cached_shape()
    {
        Type original = new { CatalogIsolation = 1 }.GetType();
        TypeNode node = new TypeNodeMapper().ToTypeNode(original);
        var registered = new TypeNodeResolver();
        registered.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original]));
        Assert.NotNull(registered.Resolve(node));
        var unregistered = new TypeNodeResolver();
        unregistered.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, []));

        Assert.Throws<InvalidOperationException>(() => unregistered.Resolve(node));
    }

    [Fact]
    public void Catalog_membership_requires_complete_descriptors()
    {
        Type original = new { CatalogFirst = 1, CatalogSecond = "two" }.GetType();
        TypeNode node = new TypeNodeMapper().ToTypeNode(original);
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original]));
        Type generated = resolver.Resolve(node);
        TypeNode[] hostile =
        [
            node with { Name = "InfoCarrier.AnonymousShape.v1/" + new string('0', 64) },
            node with { ShapeMembers = ["Changed", "CatalogSecond"] },
            node with { ShapeMembers = ["CatalogSecond", "CatalogFirst"] },
            node with { GenericArguments = node.GenericArguments.Reverse().ToArray() },
            node with { GenericArguments = [node.GenericArguments[0] with { EntityTypeName = "ForgedModel" }, node.GenericArguments[1]] },
        ];

        Assert.All(hostile, attack => Assert.Throws<InvalidOperationException>(() => resolver.Resolve(attack)));
        Assert.Same(generated, resolver.Resolve(node));
    }

    [Fact]
    public void Catalog_registration_does_not_grant_component_permissions_or_preserve_revoked_grants()
    {
        var original = new { CatalogComponent = new CatalogCard(4) };
        TypeNode node = new TypeNodeMapper().ToTypeNode(original.GetType());
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original.GetType()]));

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(node));
        resolver.UseExecutionAllowedTypes([typeof(CatalogCard)]);
        Type generated = resolver.Resolve(node);
        resolver.UseExecutionAllowedTypes([]);
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(node));
        resolver.UseExecutionAllowedTypes([typeof(CatalogCard)]);
        Assert.Same(generated, resolver.Resolve(node));
    }

    [Fact]
    public void Server_catalog_cannot_be_bypassed_with_local_original_mapper_bindings()
    {
        var original = new { CatalogLocalBinding = 1 };
        var mapper = new TypeNodeMapper();
        var resolver = new TypeNodeResolver();
        var values = new DynamicValueMapper(null, mapper, resolver);
        DynamicValueNode sent = values.ToDynamicValue(original, original.GetType());
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, []));

        Assert.Throws<InvalidOperationException>(() => values.FromDynamicValue(sent));
    }

    [Fact]
    public void Invalid_trusted_registration_does_not_return_a_partial_catalog()
    {
        Type original = new { CatalogValidBeforeInvalid = 1 }.GetType();
        Assert.Throws<InvalidOperationException>(() => AnonymousShapeCatalog.Create(null, [original, typeof(List<>)]));
        TypeNode node = new TypeNodeMapper().ToTypeNode(original);
        Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(node));
    }

    [Fact]
    public void A_valid_unregistered_shape_is_refused_before_generation()
    {
        TypeNode node = new TypeNodeMapper().ToTypeNode(new { UnregisteredCatalogValue = 1 }.GetType());

        InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
            () => new TypeNodeResolver().Resolve(node));

        Assert.Contains("catalog", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CatalogCard(int Id);
}
