// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class DocumentStoreFixture : IAsyncLifetime
{
    private ServiceProvider _serverProvider = null!;
    public string DatabaseName { get; } = "icc-" + Guid.NewGuid().ToString("N");
    protected virtual bool ServerDeclaresDocumentStore => true;
    public DbContextOptions<ShopClientContext> ClientOptions { get; private set; } = null!;
    public DbContextOptions<ShopClientContext> NonRelationalClientOptions { get; private set; } = null!;
    public IServiceProvider ServerProvider => _serverProvider;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ShopServerContext>(options => CosmosEmulator.Configure(options, DatabaseName));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopServerContext>());
        services.AddSingleton(provider =>
        {
            using IServiceScope scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ShopServerContext>();
            return AnonymousShapeCatalog.Create(context.Model,
                TrustedAnonymousShapeManifest.ForAssembly(typeof(DocumentStoreFixture).Assembly));
        });
        _serverProvider = services.BuildServiceProvider();
        _ = _serverProvider.GetRequiredService<AnonymousShapeCatalog>();
        using (IServiceScope scope = _serverProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopServerContext>();
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            db.Customers.AddRange(Seed());
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var server = new InfoCarrierEnvelopeServer(new InProcessInfoCarrierServer(_serverProvider,
            options: new InfoCarrierServerOptions().WithStoresOwnedDataInsideOwner(ServerDeclaresDocumentStore)), serializer);
        var client = new TransportInfoCarrierClient(new InProcessInfoCarrierTransport(server.DispatchAsync, serializer), serializer);
        static void ConfigureDeployment(InfoCarrierDeploymentBuilder builder)
            => builder.AddAnonymousShapes(new { Name = "", Lines = new List<OrderLine>() });
        ClientOptions = new DbContextOptionsBuilder<ShopClientContext>().UseInfoCarrier(client, ConfigureDeployment).Options;
        NonRelationalClientOptions = new DbContextOptionsBuilder<ShopClientContext>().UseInfoCarrier(client, options =>
        {
            options.ServerUsesRelationalQueryRules = false;
            ConfigureDeployment(options);
        }).Options;
    }

    public async ValueTask DisposeAsync()
    {
        if (_serverProvider is null) return;
        try
        {
            using IServiceScope scope = _serverProvider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ShopServerContext>().Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
        finally { await _serverProvider.DisposeAsync(); }
    }

    public ShopClientContext CreateClient() => new(ClientOptions);
    public ShopClientContext CreateNonRelationalClient() => new(NonRelationalClientOptions);
    public ShopServerContext CreateBackend() => new((DbContextOptions<ShopServerContext>)
        CosmosEmulator.Configure(new DbContextOptionsBuilder<ShopServerContext>(), DatabaseName).Options);
    public static Customer[] Seed() =>
    [
        new() { Id = "alice", Name = "Alice", Country = "DE", Address = new() { City = "Berlin", Postcode = "10115" },
            Lines = [new() { Sku = "book", Quantity = 2 }] },
        new() { Id = "bob", Name = "Bob", Country = "PT", Address = new() { City = "Lisbon", Postcode = "1100" },
            Lines = [new() { Sku = "book", Quantity = 1 }, new() { Sku = "lamp", Quantity = 3 }] },
        new() { Id = "carol", Name = "Carol", Country = "DE", Address = new() { City = "Hamburg", Postcode = "20095" } },
    ];
}

public sealed class UndeclaredDocumentStoreFixture : DocumentStoreFixture
{
    protected override bool ServerDeclaresDocumentStore => false;
}
