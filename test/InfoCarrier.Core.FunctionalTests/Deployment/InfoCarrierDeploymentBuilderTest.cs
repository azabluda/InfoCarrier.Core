// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.ValueMapping;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Deployment;

public class InfoCarrierDeploymentBuilderTest
{
    [Fact]
    public void Null_arrays_and_items_fail_during_configuration()
    {
        var builder = new InfoCarrierDeploymentBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddAnonymousShapes(null!));
        Assert.Throws<ArgumentException>(() => builder.AddAnonymousShapes(new object[] { null! }));
        Assert.Throws<ArgumentNullException>(() => builder.AddAllowedTypes(null!));
        Assert.Throws<ArgumentException>(() => builder.AddAllowedTypes([typeof(DateOnly), null!]));
        Assert.Throws<ArgumentNullException>(() => builder.AddValueMappers(null!));
        Assert.Throws<ArgumentException>(() => builder.AddValueMappers([new StubMapper(), null!]));
    }

    [Fact]
    public void Anonymous_prototypes_accept_nested_anonymous_types_and_reject_named_types()
    {
        var builder = new InfoCarrierDeploymentBuilder();

        builder.AddAnonymousShapes(new { Item = new { Id = 0 } });

        Assert.Throws<ArgumentException>(() => builder.AddAnonymousShapes(new NamedRecord(1)));
    }

    [Fact]
    public void Abstractions_assembly_exposes_only_the_two_approved_contracts()
        => Assert.Equal(
            [
                "InfoCarrier.Core.InfoCarrierDeploymentBuilder",
                "InfoCarrier.Core.ValueMapping.IInfoCarrierValueMapper"
            ],
            typeof(InfoCarrierDeploymentBuilder).Assembly
                .GetExportedTypes()
                .Select(type => type.FullName)
                .Order());

    [Fact]
    public void Abstractions_assembly_references_only_framework_assemblies()
        => Assert.DoesNotContain(
            typeof(InfoCarrierDeploymentBuilder).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is not null
                && (reference.Name.StartsWith("InfoCarrier", StringComparison.Ordinal)
                    || reference.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                    || reference.Name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)));

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
