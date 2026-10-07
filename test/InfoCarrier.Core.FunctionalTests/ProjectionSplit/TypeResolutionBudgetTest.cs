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
