// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.ValueMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Deployment;

public class InfoCarrierDeploymentOptionsTest
{
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
