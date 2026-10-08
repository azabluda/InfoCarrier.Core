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
        var serverResolver = new TypeNodeResolver();
        serverResolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original.GetType()]));
        var server = new DynamicValueMapper(null, serverMapper, serverResolver);
        object generated = server.FromDynamicValue(sent)!;
        Assert.Equal(original.ToString(), generated.ToString());
        Assert.Equal(original, client.FromDynamicValue(server.ToDynamicValue(generated, generated.GetType())));
    }

    [Fact]
    public void Cached_response_descriptors_are_detached_from_mutable_request_and_response_lists()
    {
        Type original = new { Snapshot = new { Id = 1 } }.GetType();
        TypeNode node = new TypeNodeMapper().ToTypeNode(original);
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original]));
        Type generated = resolver.Resolve(node);
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
            var firstResolver = new ComponentResolver(typeof(RegisteredCard));
            AnonymousShapeCatalog firstCatalog = AnonymousShapeCatalog.Create(null, [original]);
            firstResolver.UseAnonymousShapeCatalog(firstCatalog);
            var secondResolver = new ComponentResolver(card);
            secondResolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null,
                [original.GetGenericTypeDefinition().MakeGenericType(card)]));
            Type first = firstResolver.Resolve(node);
            Type second = secondResolver.Resolve(node);
            Assert.NotEqual(first, second);
            Assert.Equal(typeof(RegisteredCard), first.GetProperty("Card")!.PropertyType);
            Assert.Equal(card, second.GetProperty("Card")!.PropertyType);
            secondResolver.UseAnonymousShapeCatalog(firstCatalog);
            Assert.Throws<InvalidOperationException>(() => secondResolver.Resolve(node));
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
    public void Cached_shapes_do_not_accept_unregistered_model_aliases()
    {
        var mapper = new TypeNodeMapper();
        Type original = new { Value = new List<int>() }.GetType();
        TypeNode node = mapper.ToTypeNode(original);
        TypeNode component = mapper.ToTypeNode(typeof(List<int>));
        TypeNode first = node with { GenericArguments = [component with { EntityTypeName = "FirstModel" }] };
        TypeNode second = node with { GenericArguments = [component with { EntityTypeName = "SecondModel" }] };
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original]));
        Type generated = resolver.Resolve(node);
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(first));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(second));
        Assert.Same(generated, resolver.Resolve(node));
    }

    [Fact]
    public async Task Concurrent_resolvers_reuse_one_generated_type_for_the_same_descriptor()
    {
        Type original = new { Concurrent = 1 }.GetType();
        TypeNode node = new TypeNodeMapper().ToTypeNode(original);
        AnonymousShapeCatalog catalog = AnonymousShapeCatalog.Create(null, [original]);
        Type[] types = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() =>
            {
                var resolver = new TypeNodeResolver();
                resolver.UseAnonymousShapeCatalog(catalog);
                return resolver.Resolve(node);
            })));
        Assert.All(types, type => Assert.Same(types[0], type));
    }

    [Fact]
    public void Nested_query_syntax_carriers_keep_the_compilers_reserved_member_name()
    {
        var query = from outer in new[] { 7 }
                    from inner in new[] { 8 }
                    from last in new[] { 9 }
                    where last > 0
                    select new { outer, inner, last };
        Assert.Equal(7, query.Single().outer);
        Type carrier = typeof(AnonymousShapeProtocolTest).Assembly.GetTypes().Single(t =>
            t.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)
            && t.GetProperties().Select(p => p.Name).SequenceEqual(["<>h__TransparentIdentifier0", "last"]));
        Type nested = typeof(AnonymousShapeProtocolTest).Assembly.GetTypes().Single(t =>
            t.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)
            && t.GetProperties().Select(p => p.Name).SequenceEqual(["outer", "inner"])).MakeGenericType(typeof(int), typeof(int));
        Type original = carrier.MakeGenericType(nested, typeof(int));
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original]));
        Type generated = resolver.Resolve(new TypeNodeMapper().ToTypeNode(original));
        Assert.NotNull(generated.GetProperty("<>h__TransparentIdentifier0"));
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
        Type safe = new { Safe = 1 }.GetType();
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [safe]));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(raw));
        TypeNode shape = new TypeNodeMapper().ToTypeNode(safe);
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new TypeNode
        {
            Name = typeof(Tuple<,>).FullName!, GenericArguments = [shape, raw],
        }));
    }

    [Fact]
    public void Recursive_shape_components_do_not_grant_reflection_invocation_types()
    {
        var mapper = new TypeNodeMapper();
        Type definition = new { X = 1 }.GetType().GetGenericTypeDefinition();
        foreach (Type forbidden in new[] { typeof(System.Reflection.Binder), typeof(System.Reflection.MethodInfo),
            typeof(Activator), typeof(System.Reflection.Assembly), typeof(AppDomain) })
        {
            Type registered = definition.MakeGenericType(typeof(List<>).MakeGenericType(forbidden));
            TypeNode hostile = mapper.ToTypeNode(registered);
            var resolver = new TypeNodeResolver();
            resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [registered]));
            InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(hostile));
            Assert.Contains("allowlist", refusal.Message);
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
        Type original = new { Card = new RegisteredCard(1) }.GetType();
        TypeNode node = mapper.ToTypeNode(original);
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original, new { Safe = 1 }.GetType()]));
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
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original.GetType()]));
        Type generated = resolver.Resolve(new TypeNodeMapper().ToTypeNode(original.GetType()));
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
        Type original = new { Card = new PrivateCard(1) }.GetType();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original]));
        Type generated = resolver.Resolve(mapper.ToTypeNode(original));
        object first = Activator.CreateInstance(generated, new PrivateCard(1))!;
        object second = Activator.CreateInstance(generated, new PrivateCard(1))!;
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Exchange_shape_budget_counts_distinct_descriptors_and_resets_between_exchanges()
    {
        Type definition = new { Budget = 1 }.GetType().GetGenericTypeDefinition();
        Type[] components = [typeof(int), typeof(string), typeof(bool), typeof(long), typeof(double)];
        Type[] originals = Enumerable.Range(0, 65).Select(i =>
        {
            Type component = components[i / 13];
            for (int depth = 0; depth < i % 13; depth++)
            {
                component = typeof(List<>).MakeGenericType(component);
            }

            return definition.MakeGenericType(component);
        }).ToArray();
        TypeNode[] nodes = originals.Select(t => new TypeNodeMapper().ToTypeNode(t)).ToArray();
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, originals));
        var values = new DynamicValueMapper(null, new TypeNodeMapper(), resolver);
        for (int i = 0; i < 64; i++)
        {
            TypeNode next = nodes[i];
            Assert.Same(resolver.Resolve(next), resolver.Resolve(next));
        }

        TypeNode overflow = nodes[64];
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(overflow));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(overflow));
        values.ResetReferenceScope();
        Assert.NotNull(resolver.Resolve(overflow));
    }

    [Fact]
    public void Identity_tokens_and_ordered_member_types_prevent_shape_collisions()
    {
        var mapper = new TypeNodeMapper();
        Type original = new { A = 1, B = "two" }.GetType();
        Type reorderedOriginal = new { B = "two", A = 1 }.GetType();
        TypeNode node = mapper.ToTypeNode(original);
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original, reorderedOriginal]));
        Type first = resolver.Resolve(node);
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(node with
            { Name = "InfoCarrier.AnonymousShape.v1/" + new string('F', 64) }));
        Type reordered = resolver.Resolve(mapper.ToTypeNode(reorderedOriginal));
        Assert.NotEqual(first, reordered);
        object a = Activator.CreateInstance(first, 1, "two")!;
        object b = Activator.CreateInstance(reordered, "two", 1)!;
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
        var serverResolver = new TypeNodeResolver();
        serverResolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original[0].GetType()]));
        var server = new DynamicValueMapper(null, serverMapper, serverResolver);
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
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original.GetType()]));
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
        serverResolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original.GetType()]));
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
