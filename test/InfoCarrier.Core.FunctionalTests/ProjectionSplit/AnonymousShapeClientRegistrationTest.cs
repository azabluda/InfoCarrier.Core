// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

public class AnonymousShapeClientRegistrationTest
{
    [Fact]
    public void Client_shape_registration_rejects_named_and_null_prototypes()
    {
        var options = new DbContextOptionsBuilder();
        var builder = new InfoCarrierDbContextOptionsBuilder(options);
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => builder.UseRegisteredAnonymousShapes(new NamedRecord(1)));
        Assert.Throws<ArgumentException>(() => services.AddInfoCarrierAnonymousShapes(new NamedRecord(1)));
        Assert.Throws<ArgumentException>(() => builder.UseRegisteredAnonymousShapes(new object[] { null! }));
        Assert.Throws<ArgumentException>(() => services.AddInfoCarrierAnonymousShapes(new object[] { null! }));
    }

    [Fact]
    public void Shared_registration_keeps_existing_anonymous_validation()
    {
        var options = new DbContextOptionsBuilder();
        var client = new InfoCarrierDbContextOptionsBuilder(options);
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => client.UseInfoCarrierDeployment(
            deployment => deployment.AddAnonymousShapes(new NamedRecord(1))));
        Assert.Throws<ArgumentException>(() => services.AddInfoCarrierDeployment(
            deployment => deployment.AddAnonymousShapes(new NamedRecord(1))));
        Assert.Throws<ArgumentException>(() => client.UseInfoCarrierDeployment(
            deployment => deployment.AddAnonymousShapes(new object[] { null! })));
        Assert.Throws<ArgumentException>(() => services.AddInfoCarrierDeployment(
            deployment => deployment.AddAnonymousShapes(new object[] { null! })));
    }

    private sealed record NamedRecord(int Value);
}
