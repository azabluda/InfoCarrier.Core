// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Deployment;

public class InfoCarrierRoleRegistrationTest
{
    [Fact]
    public void Client_role_requires_an_absolute_endpoint()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfoCarrierClient(options => options.Endpoint = new Uri("relative", UriKind.Relative)));
    }

    [Fact]
    public void Client_role_registers_the_default_client()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfoCarrierClient(options =>
        {
            options.Endpoint = new Uri("https://example.test/info-carrier");
            options.AddAllowedTypes(typeof(DateOnly));
        });

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        IInfoCarrierClient client = provider.GetRequiredService<IInfoCarrierClient>();
        Assert.Same(client, provider.GetInfoCarrierClient());
        using IServiceScope scope = provider.CreateScope();
        Assert.Same(client, scope.ServiceProvider.GetRequiredService<IInfoCarrierClient>());

        DbContextOptions options = new DbContextOptionsBuilder()
            .UseApplicationServiceProvider(provider).UseInfoCarrier().Options;
        InfoCarrierOptionsExtension extension = options.FindExtension<InfoCarrierOptionsExtension>()!;
        Assert.Same(client, extension.InfoCarrierClient);
        Assert.Contains(typeof(DateOnly), extension.AllowedTypes);
    }

    [Fact]
    public async Task Server_role_registers_the_default_server_without_a_DbContext_alias()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<TestContext>();
        services.AddInfoCarrierServer<TestContext>();

        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        IInfoCarrierServer server = provider.GetRequiredService<IInfoCarrierServer>();
        Assert.Same(server, provider.GetRequiredService<IInfoCarrierServer>());
        Assert.Null(provider.GetService<DbContext>());
    }

    private sealed class TestContext : DbContext;
}
