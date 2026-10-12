// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.ValueMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Deployment;

public class InfoCarrierDeploymentOptionsTest
{
    [Fact]
    public void Server_shared_configuration_translates_all_four_facts()
    {
        var mapper = new StubMapper();
        var shape = new { Id = 0 };
        var services = new ServiceCollection();

        services.AddInfoCarrierDeployment(options =>
        {
            options.AddAnonymousShapes(shape);
            options.AddAllowedTypes(typeof(DateOnly));
            options.AddValueMappers(mapper);
            options.AllowArbitrarySqlExecution = true;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Equal(
            shape.GetType(),
            Assert.Single(provider.GetServices<InfoCarrierServiceCollectionExtensions.AnonymousShapeRegistration>())
                .Types.Single());
        Assert.Equal(
            typeof(DateOnly),
            Assert.Single(provider.GetServices<IInfoCarrierAllowedTypes>()).Types.Single());
        Assert.IsAssignableFrom<IInfoCarrierArbitrarySqlExecution>(
            provider.GetRequiredService<IInfoCarrierArbitrarySqlExecution>());
        Assert.Same(mapper, provider.GetServices<IInfoCarrierValueMapper>().First());
    }

    [Fact]
    public void Server_shared_calls_compose_with_existing_low_level_calls()
    {
        var firstShape = new { Id = 0 };
        var secondShape = new { Name = "" };
        var services = new ServiceCollection();

        services.AddInfoCarrierAnonymousShapes(firstShape);
        services.AddInfoCarrierAllowedTypes(typeof(DateOnly));
        services.AddInfoCarrierDeployment(options =>
        {
            options.AddAnonymousShapes(secondShape);
            options.AddAllowedTypes(typeof(TimeOnly));
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Equal(
            [firstShape.GetType(), secondShape.GetType()],
            provider.GetServices<InfoCarrierServiceCollectionExtensions.AnonymousShapeRegistration>()
                .SelectMany(registration => registration.Types));
        Assert.Equal(
            [typeof(DateOnly), typeof(TimeOnly)],
            provider.GetServices<IInfoCarrierAllowedTypes>().SelectMany(allowed => allowed.Types));
    }

    [Fact]
    public void Server_configuration_invokes_the_delegate_once_and_rejects_null()
    {
        var services = new ServiceCollection();
        var calls = 0;

        services.AddInfoCarrierDeployment(_ => calls++);

        Assert.Equal(1, calls);
        Assert.Throws<ArgumentNullException>(() => services.AddInfoCarrierDeployment(null!));
    }

    [Fact]
    public void Repeated_server_calls_keep_custom_order_without_standard_duplicates()
    {
        var firstMapper = new StubMapper();
        var secondMapper = new StubMapper();
        var services = new ServiceCollection();

        services.AddInfoCarrierDeployment(options => options.AddValueMappers(firstMapper));
        services.AddInfoCarrierDeployment(options => options.AddValueMappers(secondMapper));

        using ServiceProvider provider = services.BuildServiceProvider();
        IInfoCarrierValueMapper[] mappers = provider.GetServices<IInfoCarrierValueMapper>().ToArray();
        Assert.Same(firstMapper, mappers[0]);
        Assert.Same(secondMapper, mappers[1]);
        Assert.Single(mappers, mapper => mapper is IPAddressValueMapper);
        Assert.Single(mappers, mapper => mapper is UriValueMapper);
    }

    [Fact]
    public void One_application_method_configures_a_custom_mapper_on_both_roles()
    {
        var mapper = new StubMapper();
        var clientOptions = new DbContextOptionsBuilder();
        var serverServices = new ServiceCollection();

        void Configure(InfoCarrierDeploymentOptions options)
            => options.AddValueMappers(mapper);

        new InfoCarrierDbContextOptionsBuilder(clientOptions).UseInfoCarrierDeployment(Configure);
        serverServices.AddInfoCarrierDeployment(Configure);

        InfoCarrierOptionsExtension client = clientOptions.Options.FindExtension<InfoCarrierOptionsExtension>()!;
        using ServiceProvider server = serverServices.BuildServiceProvider();
        Assert.Same(mapper, Assert.Single(client.ValueMappers));
        Assert.Same(mapper, server.GetServices<IInfoCarrierValueMapper>().First());
    }

    [Fact]
    public void Client_shared_configuration_translates_all_four_facts()
    {
        var mapper = new StubMapper();
        var shape = new { Id = 0 };
        var builder = new DbContextOptionsBuilder();

        new InfoCarrierDbContextOptionsBuilder(builder).UseInfoCarrierDeployment(options =>
        {
            options.AddAnonymousShapes(shape);
            options.AddAllowedTypes(typeof(DateOnly));
            options.AddValueMappers(mapper);
            options.AllowArbitrarySqlExecution = true;
        });

        InfoCarrierOptionsExtension extension = builder.Options.FindExtension<InfoCarrierOptionsExtension>()!;
        Assert.Equal(shape.GetType(), Assert.Single(extension.RegisteredAnonymousShapes.Types));
        Assert.Equal(typeof(DateOnly), Assert.Single(extension.AllowedTypes));
        Assert.Same(mapper, Assert.Single(extension.ValueMappers));
        Assert.True(extension.ArbitrarySqlExecutionAllowed);
    }

    [Fact]
    public void Client_shared_calls_compose_with_existing_low_level_calls()
    {
        var firstShape = new { Id = 0 };
        var secondShape = new { Name = "" };
        var options = new DbContextOptionsBuilder();
        var builder = new InfoCarrierDbContextOptionsBuilder(options);

        builder.AllowTypes(typeof(DateOnly)).UseRegisteredAnonymousShapes(firstShape);
        builder.UseInfoCarrierDeployment(deployment =>
        {
            deployment.AddAnonymousShapes(secondShape);
            deployment.AddAllowedTypes(typeof(TimeOnly));
        });

        InfoCarrierOptionsExtension extension = options.Options.FindExtension<InfoCarrierOptionsExtension>()!;
        Assert.Equal([typeof(DateOnly), typeof(TimeOnly)], extension.AllowedTypes);
        Assert.Equal([firstShape.GetType(), secondShape.GetType()], extension.RegisteredAnonymousShapes.Types);
    }

    [Fact]
    public void Client_configuration_invokes_the_delegate_once_and_rejects_null()
    {
        var builder = new InfoCarrierDbContextOptionsBuilder(new DbContextOptionsBuilder());
        var calls = 0;

        builder.UseInfoCarrierDeployment(_ => calls++);

        Assert.Equal(1, calls);
        Assert.Throws<ArgumentNullException>(() => builder.UseInfoCarrierDeployment(null!));
    }

    [Fact]
    public void Different_mapper_instances_do_not_share_an_internal_provider()
    {
        var firstMapper = new StubMapper();
        var secondMapper = new StubMapper();
        InfoCarrierOptionsExtension first = ClientExtension(firstMapper);
        InfoCarrierOptionsExtension second = ClientExtension(secondMapper);

        Assert.NotEqual(0, first.Info.GetServiceProviderHashCode());
        Assert.NotEqual(0, second.Info.GetServiceProviderHashCode());
        Assert.False(first.Info.ShouldUseSameServiceProvider(second.Info));
    }

    [Fact]
    public void No_custom_mapper_keeps_the_existing_internal_provider_identity()
    {
        InfoCarrierOptionsExtension first = ClientExtension();
        InfoCarrierOptionsExtension second = ClientExtension();

        Assert.Equal(0, first.Info.GetServiceProviderHashCode());
        Assert.Equal(0, second.Info.GetServiceProviderHashCode());
        Assert.True(first.Info.ShouldUseSameServiceProvider(second.Info));
    }

    [Fact]
    public void Custom_mappers_run_before_standard_and_model_mappers()
    {
        var mapper = new StubMapper();
        InfoCarrierOptionsExtension extension = ClientExtension(mapper);
        var services = new ServiceCollection();

        extension.ApplyServices(services);

        ServiceDescriptor[] descriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IInfoCarrierValueMapper))
            .ToArray();
        Assert.Same(mapper, descriptors[0].ImplementationInstance);
        Assert.Equal(typeof(IPAddressValueMapper), descriptors[1].ImplementationType);
        Assert.Equal(typeof(UriValueMapper), descriptors[2].ImplementationType);
    }

    [Fact]
    public void Additive_methods_compose_and_preserve_order()
    {
        var firstMapper = new StubMapper();
        var secondMapper = new StubMapper();
        var firstShape = new { Id = 0 };
        var secondShape = new { Name = "" };
        var options = new InfoCarrierDeploymentOptions();

        options.AddAnonymousShapes(firstShape);
        options.AddAnonymousShapes(secondShape);
        options.AddAllowedTypes(typeof(DateOnly));
        options.AddAllowedTypes(typeof(TimeOnly));
        options.AddValueMappers(firstMapper);
        options.AddValueMappers(secondMapper);
        options.AllowArbitrarySqlExecution = true;

        Assert.Equal([firstShape.GetType(), secondShape.GetType()], options.AnonymousShapeTypes);
        Assert.Equal([typeof(DateOnly), typeof(TimeOnly)], options.AllowedTypes);
        Assert.Equal([firstMapper, secondMapper], options.ValueMappers);
        Assert.True(options.AllowArbitrarySqlExecution);
    }

    [Fact]
    public void Inputs_are_snapshotted_before_configuration_returns()
    {
        object[] shapes = [new { Id = 0 }];
        Type[] types = [typeof(DateOnly)];
        IInfoCarrierValueMapper[] mappers = [new StubMapper()];
        var options = new InfoCarrierDeploymentOptions();

        options.AddAnonymousShapes(shapes);
        options.AddAllowedTypes(types);
        options.AddValueMappers(mappers);
        shapes[0] = new { Name = "" };
        types[0] = typeof(TimeOnly);
        mappers[0] = new StubMapper();

        Assert.NotEqual(shapes[0].GetType(), Assert.Single(options.AnonymousShapeTypes));
        Assert.Equal(typeof(DateOnly), Assert.Single(options.AllowedTypes));
        Assert.NotSame(mappers[0], Assert.Single(options.ValueMappers));
    }

    [Fact]
    public void Null_arrays_and_items_fail_during_configuration()
    {
        var options = new InfoCarrierDeploymentOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddAnonymousShapes(null!));
        Assert.Throws<ArgumentException>(() => options.AddAnonymousShapes(new object[] { null! }));
        Assert.Throws<ArgumentNullException>(() => options.AddAllowedTypes(null!));
        Assert.Throws<ArgumentException>(() => options.AddAllowedTypes([typeof(DateOnly), null!]));
        Assert.Throws<ArgumentNullException>(() => options.AddValueMappers(null!));
        Assert.Throws<ArgumentException>(() => options.AddValueMappers([new StubMapper(), null!]));

        Assert.Empty(options.AnonymousShapeTypes);
        Assert.Empty(options.AllowedTypes);
        Assert.Empty(options.ValueMappers);
    }

    [Fact]
    public void Anonymous_shape_validation_matches_existing_role_APIs()
    {
        var options = new InfoCarrierDeploymentOptions();
        var client = new InfoCarrierDbContextOptionsBuilder(new DbContextOptionsBuilder());
        var server = new ServiceCollection();
        var named = new NamedRecord(1);

        ArgumentException sharedError = Assert.Throws<ArgumentException>(() => options.AddAnonymousShapes(named));
        ArgumentException clientError = Assert.Throws<ArgumentException>(() => client.UseRegisteredAnonymousShapes(named));
        ArgumentException serverError = Assert.Throws<ArgumentException>(() => server.AddInfoCarrierAnonymousShapes(named));

        Assert.Equal(clientError.Message, sharedError.Message);
        Assert.Equal(serverError.Message, sharedError.Message);
    }

    private sealed record NamedRecord(int Value);

    private static InfoCarrierOptionsExtension ClientExtension(params IInfoCarrierValueMapper[] mappers)
    {
        var options = new DbContextOptionsBuilder();
        new InfoCarrierDbContextOptionsBuilder(options).UseInfoCarrierDeployment(
            deployment => deployment.AddValueMappers(mappers));
        return options.Options.FindExtension<InfoCarrierOptionsExtension>()!;
    }

    private sealed class StubMapper : IInfoCarrierValueMapper
    {
        public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
        {
            wireValue = null;
            return false;
        }

        public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
        {
            value = null;
            return false;
        }
    }
}
