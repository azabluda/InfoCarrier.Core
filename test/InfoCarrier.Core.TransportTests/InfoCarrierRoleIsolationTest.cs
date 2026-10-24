// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class InfoCarrierRoleIsolationTest
{
    [Fact]
    public void Server_descriptors_retain_separate_policy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        InfoCarrierServerBuilder first = services.AddInfoCarrierServer<FirstContext>(options => options
            .ConfigureShared(FluentTestDeployment.Configure)
            .AddAllowedTypes(typeof(DateOnly)));
        InfoCarrierServerBuilder second = services.AddInfoCarrierServer<FirstContext>(options =>
            options.AddAnonymousShapes(new { Second = 0 }));

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        var registrations = provider.GetRequiredService<InfoCarrierServerRegistrationStore>();
        InfoCarrierServerRegistration firstRegistration = registrations.Get(first.Identity);
        InfoCarrierServerRegistration secondRegistration = registrations.Get(second.Identity);

        Assert.True(firstRegistration.AllowsArbitrarySql);
        Assert.False(secondRegistration.AllowsArbitrarySql);
        Assert.Contains(typeof(DateOnly), firstRegistration.AllowedTypes);
        Assert.DoesNotContain(typeof(DateOnly), secondRegistration.AllowedTypes);
        Assert.NotEqual(
            firstRegistration.AnonymousShapeTypes.Single(),
            secondRegistration.AnonymousShapeTypes.Single());
        Assert.Same(second.Identity, registrations.Default.Identity);
    }

    [Fact]
    public void Resolved_descriptor_store_is_immutable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        InfoCarrierServerBuilder server = services.AddInfoCarrierServer<FirstContext>();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        var registrations = provider.GetRequiredService<InfoCarrierServerRegistrationStore>();

        server.ConfigureCallerIdentity(_ => new FixedCallerIdentity());

        Assert.Null(registrations.Get(server.Identity).CallerIdentityFactory);
    }

    private sealed class FixedCallerIdentity : IInfoCarrierServerCallerIdentity
    {
        public string CurrentCallerId => "caller";
    }

    private sealed class FirstContext : DbContext;
}
