// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Runtime.Loader;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

public class AnonymousShapeStructuralRegistrationTest
{
    [Fact]
    public void Cached_membership_does_not_survive_switching_to_an_unregistered_catalog()
    {
        Type original = new { StructuralCacheScope = 1 }.GetType();
        TypeNode node = new TypeNodeMapper().ToTypeNode(original);
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original]));
        Type generated = resolver.Resolve(node);
        Assert.Same(generated, resolver.Resolve(node));
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, []));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(node));
    }

    [Fact]
    public async Task Prototype_calls_compose_snapshot_the_input_and_use_the_server_model()
    {
        var services = new ServiceCollection();
        string databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<PrototypeContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<DbContext>(p => p.GetRequiredService<PrototypeContext>());
        object[] first = [new { StructuralConfigured = 1 }];
        var serverOptions = new InfoCarrierServerOptions()
            .AddAnonymousShapes(first)
            .AddAnonymousShapes(new { StructuralSecond = "" });
        first[0] = new { NotConfigured = 2 };
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using (IServiceScope scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PrototypeContext>();
            context.Add(new PrototypeRow { Id = 1 });
            context.SaveChanges();
        }
        await using var server = new InProcessInfoCarrierServer(provider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InProcessInfoCarrierServer>.Instance,
            options: serverOptions);
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var dispatcher = new InfoCarrierEnvelopeServer(server, serializer);
        var client = new TransportInfoCarrierClient(
            new TestUtilities.InProcessInfoCarrierTransport(dispatcher.DispatchAsync, serializer), serializer);
        var options = new DbContextOptionsBuilder<PrototypeContext>().UseInfoCarrier(client, o => o
            .AddAnonymousShapes(new { StructuralConfigured = 0 }, new { StructuralSecond = "" }, new { NotConfigured = 0 }));
        using var remote = new PrototypeContext(options.Options);
        Assert.Equal(1, (await remote.Set<PrototypeRow>().Select(row => new { StructuralConfigured = row.Id }).SingleAsync()).StructuralConfigured);
        Assert.Equal("1", (await remote.Set<PrototypeRow>().Select(row => new { StructuralSecond = row.Id.ToString() }).SingleAsync()).StructuralSecond);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            remote.Set<PrototypeRow>().Select(row => new { NotConfigured = row.Id }).SingleAsync());
    }

    [Fact]
    public void Prototype_registration_rejects_named_records_and_null_prototypes()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddInfoCarrierServer<PrototypeContext>(
            builder => builder.AddAnonymousShapes(new NamedRecord(1))));
        Assert.Throws<ArgumentException>(() => services.AddInfoCarrierServer<PrototypeContext>(
            builder => builder.AddAnonymousShapes(new object[] { null! })));
    }

    [Fact]
    public void Lists_of_lists_of_anonymous_values_round_trip_with_recursive_registration()
    {
        var child = new { StructuralListItem = 42 };
        var original = new { StructuralLists = Nest(child) };
        var client = new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver());
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original.GetType()]));
        var server = new DynamicValueMapper(null, new TypeNodeMapper(), resolver);
        object generated = server.FromDynamicValue(client.ToDynamicValue(original, original.GetType()))!;
        object restored = client.FromDynamicValue(server.ToDynamicValue(generated, generated.GetType()))!;
        Assert.Equal(original.GetType(), restored.GetType());
        Assert.Equal(42, ((dynamic)restored).StructuralLists[0][0].StructuralListItem);
    }

    private static List<List<T>> Nest<T>(T value) => [[value]];
    private sealed record NamedRecord(int Id);
    private sealed class PrototypeRow
    {
        public int Id { get; set; }
    }

    private sealed class PrototypeContext(DbContextOptions<PrototypeContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<PrototypeRow>();
    }

    [Fact]
    public void Prototypes_from_another_assembly_restore_the_actual_client_type()
    {
        var original = new { StructuralRoot = new[] { new { StructuralId = 7 } } };
        var context = new AssemblyLoadContext("structural-registration", isCollectible: true);
        try
        {
            Type type = original.GetType();
            var assembly = context.LoadFromAssemblyPath(type.Assembly.Location);
            Type child = type.GetProperties()[0].PropertyType.GetElementType()!;
            Type otherChild = assembly.GetType(child.GetGenericTypeDefinition().FullName!)!.MakeGenericType(typeof(int));
            Type other = assembly.GetType(type.GetGenericTypeDefinition().FullName!)!.MakeGenericType(otherChild.MakeArrayType());
            var client = new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver());
            var resolver = new TypeNodeResolver();
            resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [other]));
            var server = new DynamicValueMapper(null, new TypeNodeMapper(), resolver);
            object generated = server.FromDynamicValue(client.ToDynamicValue(original, type))!;

            object restored = client.FromDynamicValue(server.ToDynamicValue(generated, generated.GetType()))!;
            Assert.Equal(type, restored.GetType());
            var restoredChildren = (Array)type.GetProperty("StructuralRoot")!.GetValue(restored)!;
            Assert.Equal(child, restoredChildren.GetType().GetElementType());
            Assert.Equal(7, child.GetProperty("StructuralId")!.GetValue(restoredChildren.GetValue(0)));
            var ownResolver = new TypeNodeResolver();
            ownResolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [type]));
            Assert.Same(generated.GetType(), ownResolver.Resolve(new TypeNodeMapper().ToTypeNode(type)));
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void Different_client_tokens_reuse_one_type_between_exchanges_and_echo_the_current_token()
    {
        Type original = new { StructuralAlias = 1 }.GetType();
        TypeNode node = new TypeNodeMapper().ToTypeNode(original);
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original]));
        var mapper = new TypeNodeMapper();
        var values = new DynamicValueMapper(null, mapper, resolver);
        Type generated = resolver.Resolve(node);
        Assert.Equal(node.Name, mapper.ToTypeNode(generated).Name);
        values.ResetReferenceScope();
        TypeNode alias = node with { Name = "InfoCarrier.AnonymousShape.v1/" + new string('0', 64) };

        Assert.Same(generated, resolver.Resolve(alias));
        Assert.Equal(alias.Name, mapper.ToTypeNode(generated).Name);
        Assert.Contains("identities", Assert.Throws<InvalidOperationException>(() => resolver.Resolve(node)).Message);
    }

    [Fact]
    public void Late_mapper_binding_replays_the_request_identity_and_returns_detached_snapshots()
    {
        Type original = new { StructuralSnapshot = new { Inner = 1 } }.GetType();
        TypeNode node = new TypeNodeMapper().ToTypeNode(original);
        var resolver = new TypeNodeResolver();
        resolver.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [original]));
        Type generated = resolver.Resolve(node);
        ((string[])node.GenericArguments[0].ShapeMembers!)[0] = "Mutated";
        var mapper = new TypeNodeMapper();
        _ = new DynamicValueMapper(null, mapper, resolver);
        TypeNode response = mapper.ToTypeNode(generated);

        Assert.Equal(node.Name, response.Name);
        Assert.Equal("Inner", response.GenericArguments[0].ShapeMembers![0]);
        ((string[])response.GenericArguments[0].ShapeMembers!)[0] = "AlsoMutated";
        Assert.Equal("Inner", mapper.ToTypeNode(generated).GenericArguments[0].ShapeMembers![0]);
    }
}
