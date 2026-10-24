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
        var builder = new InfoCarrierClientOptions();
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => builder.AddAnonymousShapes(new NamedRecord(1)));
        Assert.Throws<ArgumentException>(() => services.AddInfoCarrierServer<DbContext>(
            deployment => deployment.AddAnonymousShapes(new NamedRecord(1))));
        Assert.Throws<ArgumentException>(() => builder.AddAnonymousShapes(new object[] { null! }));
        Assert.Throws<ArgumentException>(() => services.AddInfoCarrierServer<DbContext>(
            deployment => deployment.AddAnonymousShapes(new object[] { null! })));
    }

    [Fact]
    public void Client_shared_registration_keeps_existing_anonymous_validation()
    {
        var client = new InfoCarrierClientOptions();

        Assert.Throws<ArgumentException>(() => client.AddAnonymousShapes(new NamedRecord(1)));
        Assert.Throws<ArgumentException>(() => client.AddAnonymousShapes(new object[] { null! }));
    }

    private sealed record NamedRecord(int Value);
}
