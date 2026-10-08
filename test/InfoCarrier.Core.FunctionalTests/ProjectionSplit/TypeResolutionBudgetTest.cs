// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

public class TypeResolutionBudgetTest
{
    [Theory]
    [InlineData(17)]
    [InlineData(128)]
    public void Raw_generic_names_cannot_bypass_the_descriptor_depth_limit(int depth)
    {
        var node = new TypeNode { Name = NestedListName(depth) };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(node));
        Assert.Contains("depth budget", error.Message);
    }

    [Fact]
    public void Mixed_name_and_descriptor_nesting_share_one_depth_budget()
    {
        TypeNode node = new() { Name = NestedListName(2) };
        for (int i = 0; i < 15; i++)
        {
            node = new TypeNode { Name = "System.Collections.Generic.List`1", GenericArguments = [node] };
        }

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(node));
        Assert.Contains("depth budget", error.Message);
    }

    [Fact]
    public void Generic_and_array_names_at_the_depth_boundary_remain_supported()
    {
        var resolver = new TypeNodeResolver();
        Type raw = resolver.Resolve(new TypeNode { Name = NestedListName(16) });
        TypeNode structured = new() { Name = "System.Int32" };
        for (int i = 0; i < 16; i++)
        {
            structured = new TypeNode { Name = "System.Collections.Generic.List`1", GenericArguments = [structured] };
        }

        Assert.Same(raw, resolver.Resolve(structured));
        Type array = typeof(List<int>[]);
        Assert.Same(array, resolver.Resolve(new TypeNodeMapper().ToTypeNode(array)));
        Assert.NotNull(resolver.Resolve(new TypeNode { Name = NestedListName(15) + "[]" }));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNode { Name = NestedListName(16) + "[]" }));
    }

    [Fact]
    public void Depth_validation_precedes_lookup_and_counts_assembly_qualified_arguments()
    {
        string name = "Missing.UnloadedLeaf, Missing.UnloadedAssembly";
        for (int i = 0; i < 17; i++)
        {
            name = "System.Collections.Generic.List`1[[" + name + "]]";
        }

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new TypeNodeResolver().Resolve(new TypeNode { Name = name }));
        Assert.Contains("depth budget", error.Message);
    }

    [Fact]
    public void Long_wide_and_malformed_names_are_refused_before_runtime_resolution()
    {
        var resolver = new TypeNodeResolver();
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNode { Name = new string('X', 16385) }));
        string wide = "Missing.Generic`1024[" + string.Join(",", Enumerable.Repeat("System.Int32", 1024)) + "]";
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNode { Name = wide }));
        Assert.Contains("complex CLR type name", error.Message);
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNode { Name = "System.Int32[" }));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNode
        {
            Name = NestedListName(1), GenericArguments = [new TypeNode { Name = "System.Int32" }],
        }));
    }

    [Fact]
    public void Structured_breadth_is_refused_before_the_definition_is_looked_up()
    {
        var node = new TypeNode
        {
            Name = "Missing.Generic`1024",
            GenericArguments = Enumerable.Repeat(new TypeNode { Name = "System.Int32" }, 1024).ToArray(),
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(node));
        Assert.Contains("node budget", error.Message);
    }

    [Fact]
    public void Separate_raw_names_share_the_descriptor_node_budget()
    {
        // 255 structured definitions plus 256 three-node List<int> names: 1023 parser nodes.
        TypeNode node = BinaryDictionary(8, new TypeNode { Name = NestedListName(1) });
        Assert.NotNull(new TypeNodeResolver().Resolve(node));

        // Another definition and leaf make 1025 nodes, although each name is small.
        TypeNode oversized = new()
        {
            Name = "System.Collections.Generic.Dictionary`2",
            GenericArguments = [node, new TypeNode { Name = "Missing.Leaf" }],
        };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(oversized));
        Assert.Contains("node budget", error.Message);
    }

    [Fact]
    public void A_late_deep_argument_is_validated_before_an_unavailable_definition()
    {
        var node = new TypeNode
        {
            Name = "Missing.Generic`2",
            GenericArguments = [new TypeNode { Name = "System.Int32" }, new TypeNode { Name = NestedListName(16) }],
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(node));
        Assert.Contains("depth budget", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("System.Int32[")]
    public void Malformed_names_have_a_descriptor_rejection(string? name)
    {
        var node = new TypeNode { Name = name! };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(node));
        Assert.Contains("CLR type name", error.Message);
    }

    [Fact]
    public void A_null_argument_list_is_refused_before_cache_key_traversal()
    {
        Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(new TypeNode
        {
            Name = "System.Int32", GenericArguments = null!,
        }));
    }

    [Fact]
    public void A_null_argument_is_refused_before_cache_key_traversal()
    {
        Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(new TypeNode
        {
            Name = "System.Collections.Generic.List`1", GenericArguments = [null!],
        }));
    }

    [Fact]
    public void Entity_name_length_is_bounded_before_cache_key_traversal()
    {
        var resolver = new TypeNodeResolver();
        Assert.Same(typeof(int), resolver.Resolve(new TypeNode
        {
            Name = "System.Int32", EntityTypeName = new string('X', 16384),
        }));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNode
        {
            Name = "System.Int32", EntityTypeName = new string('X', 16385),
        }));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[,]")]
    [InlineData("*")]
    [InlineData("&")]
    public void Element_modifiers_consume_the_same_depth_budget(string modifier)
    {
        var resolver = new TypeNodeResolver();
        Assert.NotNull(resolver.Resolve(new TypeNode { Name = NestedListName(15) + modifier }));
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(new TypeNode { Name = NestedListName(16) + modifier }));
        Assert.Contains("depth budget", error.Message);
    }

    [Fact]
    public void Exact_generic_registration_remains_a_whole_type_permission_and_can_be_revoked()
    {
        var resolver = new TypeNodeResolver();
        Type type = typeof(Envelope<PrivateArgument>);
        var mapper = new TypeNodeMapper();
        resolver.UseExecutionAllowedTypes([type]);

        Assert.Same(type, resolver.Resolve(mapper.ToTypeNode(type)));
        Assert.Same(type, resolver.Resolve(new TypeNode { Name = type.AssemblyQualifiedName! }));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(mapper.ToTypeNode(typeof(PrivateArgument))));

        resolver.UseExecutionAllowedTypes([]);
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(mapper.ToTypeNode(type)));
    }

    private sealed class PrivateArgument;
    private sealed class Envelope<T>;

    private static TypeNode BinaryDictionary(int depth, TypeNode leaf)
        => depth == 0 ? leaf : new TypeNode
        {
            Name = "System.Collections.Generic.Dictionary`2",
            GenericArguments = [BinaryDictionary(depth - 1, leaf), BinaryDictionary(depth - 1, leaf)],
        };

    private static string NestedListName(int depth)
    {
        string name = "System.Int32";
        for (int i = 0; i < depth; i++)
        {
            name = "System.Collections.Generic.List`1[" + name + "]";
        }

        return name;
    }
}
