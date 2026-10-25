// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Context = InfoCarrier.Core.TransportTests.ClientServerCoexistenceTest.Context;
using Item = InfoCarrier.Core.TransportTests.ClientServerCoexistenceTest.Item;

namespace InfoCarrier.Core.TransportTests;

public class NamedClientRegistrationTest
{
    [Theory]
    [InlineData("context")]
    [InlineData("factory")]
    [InlineData("pool")]
    [InlineData("pooled-factory")]
    public async Task Standard_registrations_select_a_named_client(string method)
    {
        await using var host = await Host.Start();
        var services = new ServiceCollection();
        switch (method)
        {
            case "context": services.AddDbContext<Context>(o => o.UseInfoCarrier(clientName: "orders")); break;
            case "factory": services.AddDbContextFactory<Context>(o => o.UseInfoCarrier(clientName: "orders")); break;
            case "pool": services.AddDbContextPool<Context>(o => o.UseInfoCarrier(clientName: "orders")); break;
            case "pooled-factory": services.AddPooledDbContextFactory<Context>(o => o.UseInfoCarrier(clientName: "orders")); break;
        }
        // The options callback runs after registration, even when the client is registered last.
        Register(services, "orders", host);
        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        for (var lease = 0; lease < 2; lease++)
        {
            using IServiceScope scope = provider.CreateScope();
            await using Context context = method.Contains("factory", StringComparison.Ordinal)
                ? await scope.ServiceProvider.GetRequiredService<IDbContextFactory<Context>>().CreateDbContextAsync()
                : scope.ServiceProvider.GetRequiredService<Context>();
            Assert.Empty(await context.Items.ToListAsync());
        }
        Assert.Null(provider.GetService<IInfoCarrierClient>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Same_context_clients_keep_handlers_and_stored_data_separate(bool reverse)
    {
        await using var first = await Host.Start();
        await using var second = await Host.Start();
        var services = new ServiceCollection();
        services.AddHttpClient("orders", _ => throw new InvalidOperationException("Application-owned name was used."));
        if (reverse) { Register(services, "catalog", second); }
        Register(services, "orders", first);
        if (!reverse) { Register(services, "catalog", second); }
        services.AddKeyedSingleton<IDbContextFactory<Context>>("first", (sp, _) => new Factory(
            new DbContextOptionsBuilder<Context>().UseApplicationServiceProvider(sp).UseInfoCarrier("orders").Options));
        services.AddKeyedSingleton<IDbContextFactory<Context>>("second", (sp, _) => new Factory(
            new DbContextOptionsBuilder<Context>().UseApplicationServiceProvider(sp).UseInfoCarrier("catalog").Options));
        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await Task.WhenAll(
            Write(provider.GetRequiredKeyedService<IDbContextFactory<Context>>("first"), "orders"),
            Write(provider.GetRequiredKeyedService<IDbContextFactory<Context>>("second"), "catalog"));
        await first.Verify("orders-updated");
        await second.Verify("catalog-updated");
    }

    [Fact]
    public void Names_are_case_sensitive_and_never_supply_a_default()
    {
        var services = new ServiceCollection();
        services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://orders"));
        services.AddInfoCarrierClient("Orders", o => o.WithEndpoint("http://other"));
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        var options = new DbContextOptionsBuilder<Context>().UseApplicationServiceProvider(provider);
        IInfoCarrierClient first = Attached(options.UseInfoCarrier("orders"));
        IInfoCarrierClient second = Attached(options.UseInfoCarrier("Orders"));
        Assert.NotSame(first, second);
        Assert.Same(first, Attached(options.UseInfoCarrier("orders")));
        Assert.Contains("No default", Assert.Throws<InvalidOperationException>(() => options.UseInfoCarrier()).Message);
        Assert.Contains("ORDERS", Assert.Throws<InvalidOperationException>(() => options.UseInfoCarrier("ORDERS")).Message);
    }

    [Fact]
    public void Duplicate_name_is_rejected_before_its_callback()
    {
        var services = new ServiceCollection();
        services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://orders"));
        bool invoked = false;
        var failure = Assert.Throws<InvalidOperationException>(() => services.AddInfoCarrierClient("orders", _ => invoked = true));
        Assert.Contains("orders", failure.Message);
        Assert.False(invoked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Invalid_names_are_rejected(string? name)
    {
        var services = new ServiceCollection();
        Assert.ThrowsAny<ArgumentException>(() => services.AddInfoCarrierClient(name!, _ => { }));
        Assert.ThrowsAny<ArgumentException>(() => new DbContextOptionsBuilder<Context>().UseInfoCarrier(clientName: name!));
    }

    [Fact]
    public void Missing_name_and_application_provider_have_actionable_errors()
    {
        Assert.Contains("UseApplicationServiceProvider", Assert.Throws<InvalidOperationException>(() =>
            new DbContextOptionsBuilder<Context>().UseInfoCarrier("orders")).Message);
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var failure = Assert.Throws<InvalidOperationException>(() => new DbContextOptionsBuilder<Context>()
            .UseApplicationServiceProvider(provider).UseInfoCarrier("orders"));
        Assert.Contains("orders", failure.Message);
        Assert.Contains("AddInfoCarrierClient", failure.Message);
    }

    [Fact]
    public async Task Default_replacement_does_not_compose_old_handlers_or_change_named_selection()
    {
        await using var host = await Host.Start();
        var services = new ServiceCollection();
        IHttpClientBuilder old = services.AddInfoCarrierClient(o => o.WithEndpoint("http://old"))
            .ConfigureHttpClient(_ => throw new InvalidOperationException("Replaced configuration was used."));
        Register(services, "orders", host);
        IHttpClientBuilder current = services.AddInfoCarrierClient(o => o.WithEndpoint("http://localhost/infocarrier"))
            .ConfigurePrimaryHttpMessageHandler(() => host.App.GetTestServer().CreateHandler());
        Assert.NotEqual(old.Name, current.Name);
        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        var options = new DbContextOptionsBuilder<Context>().UseApplicationServiceProvider(provider);
        IInfoCarrierClient named = Attached(options.UseInfoCarrier("orders"));
        Assert.NotSame(named, provider.GetRequiredService<IInfoCarrierClient>());
        await using var context = new Context(options.UseInfoCarrier().Options);
        Assert.Empty(await context.Items.ToListAsync());
    }

    [Fact]
    public void Named_registration_preserves_default_and_snapshotted_policy()
    {
        var services = new ServiceCollection();
        services.AddInfoCarrierClient(o => o.WithEndpoint("http://default"));
        InfoCarrierClientOptions? mutable = null;
        services.AddInfoCarrierClient("orders", o =>
        {
            mutable = o;
            o.WithEndpoint("http://orders").WithMaxResponseBytes(1234)
                .WithServerUsesRelationalQueryRules(false).AddAllowedTypes(typeof(DateOnly));
        });
        mutable!.MaxResponseBytes = 9999;
        using ServiceProvider provider = services.BuildServiceProvider();
        DbContextOptionsBuilder builder = new DbContextOptionsBuilder().UseApplicationServiceProvider(provider);
        Assert.Same(provider.GetRequiredService<IInfoCarrierClient>(), Attached(builder.UseInfoCarrier()));
        builder.UseInfoCarrier(clientName: "orders");
        InfoCarrierClientRegistration registration = ((IInfoCarrierClientConfiguration)Attached(builder)).Registration!;
        Assert.Equal(new Uri("http://orders"), registration.Endpoint);
        Assert.Equal(1234, registration.MaxResponseBytes);
        Assert.False(builder.Options.FindExtension<InfoCarrierOptionsExtension>()!.ServerStoreIsRelational);
        Assert.Contains(typeof(DateOnly), builder.Options.FindExtension<InfoCarrierOptionsExtension>()!.AllowedTypes);
    }

    [Fact]
    public async Task Host_disposes_the_activated_http_client_wrapper()
    {
        var services = new ServiceCollection();
        HttpClient? http = null;
        services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://orders"))
            .ConfigureHttpClient(client => http = client);
        using (ServiceProvider provider = services.BuildServiceProvider())
        {
            new DbContextOptionsBuilder().UseApplicationServiceProvider(provider).UseInfoCarrier("orders");
            Assert.NotNull(http);
        }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => http!.GetAsync("http://orders"));
    }

    [Fact]
    public void Runtime_name_catalog_is_a_snapshot()
    {
        var services = new ServiceCollection();
        services.AddInfoCarrierClient("first", o => o.WithEndpoint("http://first"));
        using ServiceProvider provider = services.BuildServiceProvider();
        InfoCarrierClientRegistrationStore store = provider.GetRequiredService<InfoCarrierClientRegistrationStore>();
        services.AddInfoCarrierClient("later", o => o.WithEndpoint("http://later"));
        Assert.Contains("later", Assert.Throws<InvalidOperationException>(() => store.Get(provider, "later")).Message);
    }

    private static IInfoCarrierClient Attached(DbContextOptionsBuilder builder)
        => builder.Options.FindExtension<InfoCarrierOptionsExtension>()!.InfoCarrierClient!;

    private static void Register(IServiceCollection services, string name, Host host)
        => services.AddInfoCarrierClient(name, o => o.WithEndpoint("http://localhost/infocarrier"))
            .ConfigureHttpClient(http => http.DefaultRequestHeaders.Add("X-Registration", name))
            .ConfigurePrimaryHttpMessageHandler(() => new CheckHeaderHandler(name, host.App.GetTestServer().CreateHandler()));

    private static async Task Write(IDbContextFactory<Context> factory, string name)
    {
        await using (Context context = factory.CreateDbContext())
        {
            context.Add(new Item { Id = 1, Name = name });
            Assert.Equal(1, await context.SaveChangesAsync());
        }
        await using (Context context = factory.CreateDbContext())
        {
            Item item = await context.Items.SingleAsync();
            Assert.Equal(name, item.Name);
            Assert.Equal(EntityState.Unchanged, context.Entry(item).State);
            item.Name += "-updated";
            Assert.Equal(1, await context.SaveChangesAsync());
        }
    }

    private sealed class Factory(DbContextOptions<Context> options) : IDbContextFactory<Context>
    {
        public Context CreateDbContext() => new(options);
    }

    private sealed class CheckHeaderHandler(string name, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(name, Assert.Single(request.Headers.GetValues("X-Registration")));
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class Host(WebApplication app, string path) : IAsyncDisposable
    {
        internal WebApplication App { get; } = app;

        internal static async Task<Host> Start()
        {
            string path = Path.Combine(Path.GetTempPath(), $"icc-named-{Guid.NewGuid():N}.db");
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddDbContext<Context>(o => o.UseSqlite($"Data Source={path};Pooling=false"));
            builder.Services.AddInfoCarrierServer<Context>();
            WebApplication app = builder.Build();
            app.MapInfoCarrier();
            using (IServiceScope scope = app.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<Context>().Database.EnsureCreatedAsync();
            }
            await app.StartAsync();
            return new Host(app, path);
        }

        internal async Task Verify(string name)
        {
            using IServiceScope scope = App.Services.CreateScope();
            Assert.Equal(name, (await scope.ServiceProvider.GetRequiredService<Context>().Items.SingleAsync()).Name);
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }
}
