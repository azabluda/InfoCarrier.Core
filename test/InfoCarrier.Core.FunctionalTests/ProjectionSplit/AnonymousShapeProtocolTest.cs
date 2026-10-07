// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Text.Json;
using InfoCarrier.Core.Expressions;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

public class AnonymousShapeProtocolTest
{
    [Fact]
    public void Compiler_identifiers_with_combining_marks_and_letter_numbers_round_trip()
    {
        var original = new { e\u0301 = 1, \u2167 = 2 };
        var mapper = new TypeNodeMapper();
        var client = new DynamicValueMapper(null, mapper, new TypeNodeResolver());
        DynamicValueNode sent = client.ToDynamicValue(original, original.GetType());
        var serverMapper = new TypeNodeMapper();
        var server = new DynamicValueMapper(null, serverMapper, new TypeNodeResolver());
        object generated = server.FromDynamicValue(sent)!;
        Assert.Equal(original.ToString(), generated.ToString());
        Assert.Equal(original, client.FromDynamicValue(server.ToDynamicValue(generated, generated.GetType())));
    }

    [Fact]
    public void Cached_response_descriptors_are_detached_from_mutable_request_and_response_lists()
    {
        TypeNode node = new TypeNodeMapper().ToTypeNode(new { Snapshot = new { Id = 1 } }.GetType());
        Type generated = new TypeNodeResolver().Resolve(node);
        ((string[])node.GenericArguments[0].ShapeMembers!)[0] = "ChangedRequest";
        var mapper = new TypeNodeMapper();
        TypeNode response = mapper.ToTypeNode(generated);
        Assert.Equal("Id", response.GenericArguments[0].ShapeMembers![0]);
        ((string[])response.GenericArguments[0].ShapeMembers!)[0] = "ChangedResponse";
        Assert.Equal("Id", mapper.ToTypeNode(generated).GenericArguments[0].ShapeMembers![0]);
    }

    [Fact]
    public void Separate_assembly_load_contexts_keep_original_and_component_type_identities()
    {
        var context = new System.Runtime.Loader.AssemblyLoadContext("anonymous-shape-test", isCollectible: true);
        try
        {
            System.Reflection.Assembly duplicate = context.LoadFromAssemblyPath(typeof(RegisteredCard).Assembly.Location);
            Type card = duplicate.GetType(typeof(RegisteredCard).FullName!)!;
            Type original = new { Card = new RegisteredCard(1) }.GetType();
            Type otherOriginal = duplicate.GetType(original.GetGenericTypeDefinition().FullName!)!.MakeGenericType(card);
            Assert.NotEqual(new TypeNodeMapper().ToTypeNode(original).Name,
                new TypeNodeMapper().ToTypeNode(otherOriginal).Name);
            TypeNode node = new TypeNodeMapper().ToTypeNode(original);
            Type first = new ComponentResolver(typeof(RegisteredCard)).Resolve(node);
            Type second = new ComponentResolver(card).Resolve(node);
            Assert.NotEqual(first, second);
            Assert.Equal(typeof(RegisteredCard), first.GetProperty("Card")!.PropertyType);
            Assert.Equal(card, second.GetProperty("Card")!.PropertyType);
        }
        finally
        {
            context.Unload();
        }
    }

    private sealed class ComponentResolver(Type component)
        : TypeNodeResolver(null, TypeAllowlist.ForModel(null, [component]))
    {
        public override Type Resolve(TypeNode node)
            => node.Name == typeof(RegisteredCard).FullName ? component : base.Resolve(node);
    }

    [Fact]
    public void Cached_shapes_do_not_return_another_models_component_identity()
    {
        var mapper = new TypeNodeMapper();
        TypeNode node = mapper.ToTypeNode(new { Value = 1 }.GetType());
        TypeNode component = mapper.ToTypeNode(typeof(List<int>));
        TypeNode first = node with { GenericArguments = [component with { EntityTypeName = "FirstModel" }] };
        TypeNode second = node with { GenericArguments = [component with { EntityTypeName = "SecondModel" }] };
        var resolver = new TypeNodeResolver();
        Type firstType = resolver.Resolve(first);
        Type secondType = resolver.Resolve(second);
        Assert.NotEqual(firstType, secondType);
        Assert.Equal("FirstModel", new TypeNodeMapper().ToTypeNode(firstType).GenericArguments[0].EntityTypeName);
        Assert.Equal("SecondModel", new TypeNodeMapper().ToTypeNode(secondType).GenericArguments[0].EntityTypeName);
    }

    [Fact]
    public async Task Concurrent_resolvers_reuse_one_generated_type_for_the_same_descriptor()
    {
        TypeNode node = new TypeNodeMapper().ToTypeNode(new { Concurrent = 1 }.GetType());
        Type[] types = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => new TypeNodeResolver().Resolve(node))));
        Assert.All(types, type => Assert.Same(types[0], type));
    }

    [Fact]
    public void Nested_query_syntax_carriers_keep_the_compilers_reserved_member_name()
    {
        TypeNode node = new TypeNodeMapper().ToTypeNode(new { Outer = 1 }.GetType())
            with { ShapeMembers = ["<>h__TransparentIdentifier0"] };
        Type generated = new TypeNodeResolver().Resolve(node);
        object value = Activator.CreateInstance(generated, 7)!;
        Assert.Equal(7, generated.GetProperty("<>h__TransparentIdentifier0")!.GetValue(value));
    }

    [Fact]
    public void A_raw_client_anonymous_name_is_refused_even_when_its_assembly_is_loaded()
    {
        Type type = new { X = 1 }.GetType();
        var raw = new TypeNode
        {
            Name = type.GetGenericTypeDefinition().FullName!,
            GenericArguments = [new TypeNode { Name = typeof(int).FullName! }],
        };
        Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(raw));
        TypeNode shape = new TypeNodeMapper().ToTypeNode(new { Safe = 1 }.GetType());
        Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(new TypeNode
        {
            Name = typeof(Tuple<,>).FullName!, GenericArguments = [shape, raw],
        }));
    }

    [Fact]
    public void Recursive_shape_components_do_not_grant_reflection_invocation_types()
    {
        var mapper = new TypeNodeMapper();
        TypeNode shape = mapper.ToTypeNode(new { X = 1 }.GetType());
        foreach (Type forbidden in new[] { typeof(System.Reflection.Binder), typeof(System.Reflection.MethodInfo),
            typeof(Activator), typeof(System.Reflection.Assembly), typeof(AppDomain) })
        {
            var hostile = shape with { GenericArguments = [mapper.ToTypeNode(typeof(List<>).MakeGenericType(forbidden))] };
            Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(hostile));
        }
    }

    [Fact]
    public void Shape_metadata_rejects_duplicates_arity_mismatch_excess_names_and_excess_depth()
    {
        var mapper = new TypeNodeMapper();
        TypeNode shape = mapper.ToTypeNode(new { X = 1 }.GetType());
        foreach (TypeNode bad in new[]
        {
            shape with { ShapeMembers = ["X", "X"], GenericArguments = [shape.GenericArguments[0], shape.GenericArguments[0]] },
            shape with { ShapeMembers = [] },
            shape with { ShapeMembers = [new string('X', 129)] },
            shape with { ShapeMembers = ["X); Process.Start()"] },
            shape with { ShapeMembers = Enumerable.Range(0, 33).Select(i => "X" + i).ToArray(),
                GenericArguments = Enumerable.Repeat(shape.GenericArguments[0], 33).ToArray() },
            shape with { GenericArguments = [new TypeNode { Name = "System.Void" }] },
            shape with { EntityTypeName = "Entity" },
        })
        {
            Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(bad));
        }

        TypeNode deep = shape;
        for (int i = 0; i < 18; i++)
        {
            deep = shape with { GenericArguments = [deep] };
        }

        Assert.Throws<InvalidOperationException>(() => new TypeNodeResolver().Resolve(deep));
    }

    [Fact]
    public void Shape_permission_is_rechecked_after_execution_registration_changes()
    {
        var mapper = new TypeNodeMapper();
        TypeNode node = mapper.ToTypeNode(new { Card = new RegisteredCard(1) }.GetType());
        var resolver = new TypeNodeResolver();
        resolver.UseExecutionAllowedTypes([typeof(RegisteredCard)]);
        Type generated = resolver.Resolve(node);
        Assert.Same(generated, resolver.Resolve(node));
        resolver.UseExecutionAllowedTypes([]);
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(node));
        var rawDefinition = new TypeNode
        {
            Name = generated.GetGenericTypeDefinition().FullName!,
            GenericArguments = node.GenericArguments,
        };
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNode
        {
            Name = typeof(Tuple<,>).FullName!,
            GenericArguments = [new TypeNodeMapper().ToTypeNode(new { Safe = 1 }.GetType()), rawDefinition],
        }));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNode
        {
            Name = typeof(Tuple<,>).FullName!,
            GenericArguments = [new TypeNodeMapper().ToTypeNode(new { Safe = 1 }.GetType()),
                new TypeNode { Name = generated.FullName! }],
        }));
    }

    public sealed record RegisteredCard(int Id);

    private sealed record PrivateCard(int Id);
    private enum PrivateCode { First, Second }

    [Fact]
    public void Generated_equality_hash_and_formatting_support_private_enum_components()
    {
        var original = new { Code = PrivateCode.Second };
        Type generated = new TypeNodeResolver().Resolve(new TypeNodeMapper().ToTypeNode(original.GetType()));
        object first = Activator.CreateInstance(generated, PrivateCode.Second)!;
        object second = Activator.CreateInstance(generated, PrivateCode.Second)!;
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(original.ToString(), first.ToString());
    }

    [Fact]
    public void Generated_equality_supports_explicitly_registered_private_component_types()
    {
        var mapper = new TypeNodeMapper();
        var resolver = new TypeNodeResolver(null, TypeAllowlist.ForModel(null, [typeof(PrivateCard)]));
        Type generated = resolver.Resolve(mapper.ToTypeNode(new { Card = new PrivateCard(1) }.GetType()));
        object first = Activator.CreateInstance(generated, new PrivateCard(1))!;
        object second = Activator.CreateInstance(generated, new PrivateCard(1))!;
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Exchange_shape_budget_counts_distinct_descriptors_and_resets_between_exchanges()
    {
        var mapper = new TypeNodeMapper();
        TypeNode node = mapper.ToTypeNode(new { Budget = 1 }.GetType());
        var resolver = new TypeNodeResolver();
        var values = new DynamicValueMapper(null, new TypeNodeMapper(), resolver);
        for (int i = 0; i < 64; i++)
        {
            TypeNode next = node with { Name = "InfoCarrier.AnonymousShape.v1/" + i.ToString("X64") };
            Assert.Same(resolver.Resolve(next), resolver.Resolve(next));
        }

        TypeNode overflow = node with { Name = "InfoCarrier.AnonymousShape.v1/" + 64.ToString("X64") };
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(overflow));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(overflow));
        values.ResetReferenceScope();
        Assert.NotNull(resolver.Resolve(overflow));
    }

    [Fact]
    public void Identity_tokens_and_ordered_member_types_prevent_shape_collisions()
    {
        var mapper = new TypeNodeMapper();
        TypeNode node = mapper.ToTypeNode(new { A = 1, B = "two" }.GetType());
        var resolver = new TypeNodeResolver();
        Type first = resolver.Resolve(node);
        Type second = resolver.Resolve(node with { Name = "InfoCarrier.AnonymousShape.v1/" + new string('F', 64) });
        Type reordered = resolver.Resolve(node with { ShapeMembers = ["B", "A"],
            GenericArguments = node.GenericArguments.Reverse().ToArray() });
        Assert.NotEqual(first, second);
        Assert.NotEqual(first, reordered);
        object a = Activator.CreateInstance(first, 1, "two")!;
        object b = Activator.CreateInstance(second, 1, "two")!;
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Anonymous_arrays_round_trip_as_the_original_client_element_type()
    {
        var mapper = new TypeNodeMapper();
        var resolver = new TypeNodeResolver();
        var client = new DynamicValueMapper(null, mapper, resolver);
        var original = new[] { new { Id = 1 }, new { Id = 2 } };
        DynamicValueNode sent = client.ToDynamicValue(original, original.GetType());
        var serverMapper = new TypeNodeMapper();
        var server = new DynamicValueMapper(null, serverMapper, new TypeNodeResolver());
        object rebuilt = server.FromDynamicValue(sent)!;
        Assert.NotEqual(original.GetType(), rebuilt.GetType());
        Assert.Equal(original, client.FromDynamicValue(server.ToDynamicValue(rebuilt, rebuilt.GetType())));
    }

    [Fact]
    public void Generated_shapes_keep_case_distinct_names_reference_equality_and_structural_values()
    {
        var mapper = new TypeNodeMapper();
        var resolver = new TypeNodeResolver();
        var original = new { X = 1, x = 2, Empty = new { } };
        Type generated = resolver.Resolve(mapper.ToTypeNode(original.GetType()));
        Type empty = generated.GetProperty("Empty")!.PropertyType;
        object a = Activator.CreateInstance(generated, 1, 2, Activator.CreateInstance(empty))!;
        object b = Activator.CreateInstance(generated, 1, 2, Activator.CreateInstance(empty))!;
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(ReferenceEquals(a, b));
        Assert.False(a.Equals(original));
        Assert.Equal(original.ToString(), a.ToString());
        Assert.All(generated.GetProperties(), p => Assert.Null(p.SetMethod));
        var clientResolver = new TypeNodeResolver();
        var clientValues = new DynamicValueMapper(null, mapper, clientResolver);
        var serverMapper = new TypeNodeMapper();
        var serverValues = new DynamicValueMapper(null, serverMapper, resolver);
        Assert.Equal(original, clientValues.FromDynamicValue(serverValues.ToDynamicValue(a, generated)));
    }

    [Fact]
    public void Separate_pipelines_reconstruct_server_shapes_and_restore_original_client_types()
    {
        var mapper = new TypeNodeMapper();
        var resolver = new TypeNodeResolver();
        var values = new DynamicValueMapper(null, mapper, resolver);
        var serverMapper = new TypeNodeMapper();
        var serverResolver = new TypeNodeResolver();
        var serverValues = new DynamicValueMapper(null, serverMapper, serverResolver);
        var original = new { Name = "one", Nested = new { Id = 3 }, Missing = (string?)null };
        Expression<Func<object>> query = () => new { Name = "one", Nested = new { Id = 3 }, Missing = (string?)null };
        ExpressionNode sent = new ExpressionToNodeTranslator(mapper, values).Translate(query);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(sent, ExpressionJsonContext.Default.ExpressionNode);
        ExpressionNode received = JsonSerializer.Deserialize(bytes, ExpressionJsonContext.Default.ExpressionNode)!;
        var rebuilt = (LambdaExpression)new NodeToExpressionTranslator(serverResolver, serverValues,
            (_, _) => throw new InvalidOperationException()).Translate(received);
        object server = rebuilt.Compile().DynamicInvoke()!;
        Assert.NotEqual(original.GetType(), server.GetType());
        Assert.Equal(original.ToString(), server.ToString());
        DynamicValueNode row = serverValues.ToDynamicValue(server, server.GetType());
        byte[] response = JsonSerializer.SerializeToUtf8Bytes(row, ExpressionJsonContext.Default.DynamicValueNode);
        object restored = values.FromDynamicValue(JsonSerializer.Deserialize(response,
            ExpressionJsonContext.Default.DynamicValueNode)!)!;
        Assert.Equal(original.GetType(), restored.GetType());
        Assert.Equal(original, restored);
    }
}
