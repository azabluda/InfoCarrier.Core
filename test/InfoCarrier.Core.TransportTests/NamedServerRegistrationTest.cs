// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Net;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class NamedServerRegistrationTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Same_context_endpoints_use_selected_scoped_contexts_and_sql_grants(bool byHandle)
    {
        await using var host = await Host.Start(byHandle);
        using HttpClient http = host.App.GetTestClient();
        IInfoCarrierClient admin = Client(http, "admin");
        IInfoCarrierClient publicClient = Client(http, "public");
        await Task.WhenAll(Write(admin, "admin"), Write(publicClient, "public"));
        await host.Verify("admin");
        await host.Verify("public");

        await using (var context = ContextFor(admin))
        {
            Assert.Equal(1, await context.Database.SqlQueryRaw<int>("SELECT 1 AS Value").SingleAsync());
        }
        await using (var context = ContextFor(publicClient))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.Database.SqlQueryRaw<int>("SELECT 1 AS Value").SingleAsync());
        }
        Assert.All(host.Contexts, context => Assert.Equal(1, context.Disposals));
    }

    [Fact]
    public async Task Transaction_from_one_mapped_endpoint_is_refused_by_the_other()
    {
        await using var host = await Host.Start(false);
        using HttpClient http = host.App.GetTestClient();
        IInfoCarrierClient admin = Client(http, "admin");
        IInfoCarrierClient publicClient = Client(http, "public");
        string token = (await admin.BeginTransactionAsync()).TransactionId!;
        try
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => publicClient.CommitTransactionAsync(token));
            Assert.Contains("different server", failure.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await admin.RollbackTransactionAsync(token);
        }
        Assert.All(host.Contexts, context => Assert.Equal(1, context.Disposals));
    }

    [Fact]
    public async Task Mapped_endpoints_use_their_own_request_serializer_limits()
    {
        await using var host = await Host.Start(false);
        using HttpClient http = host.App.GetTestClient();
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        byte[] body = serializer.Serialize(new InfoCarrierEnvelope
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = InfoCarrierOperation.BeginTransaction,
            Payload = new byte[32 * 1024],
        });
        using HttpResponseMessage rejected = await http.PostAsync("/public", new ByteArrayContent(body));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using HttpResponseMessage accepted = await http.PostAsync("/admin", new ByteArrayContent(body));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        InfoCarrierEnvelope envelope = serializer.Deserialize<InfoCarrierEnvelope>(await accepted.Content.ReadAsByteArrayAsync())!;
        Assert.Null(envelope.Fault);
        string token = serializer.Deserialize<TransactionResult>(envelope.Payload)!.TransactionId;
        await Client(http, "admin").RollbackTransactionAsync(token);
    }

    [Fact]
    public async Task Host_disposal_releases_transactions_from_every_activated_endpoint()
    {
        var host = await Host.Start(false);
        using HttpClient http = host.App.GetTestClient();
        await Client(http, "admin").BeginTransactionAsync();
        await Client(http, "public").BeginTransactionAsync();
        Assert.Equal(2, host.Contexts.Count(context => context.Disposals == 0));
        await host.DisposeAsync();
        Assert.All(host.Contexts, context => Assert.Equal(1, context.Disposals));
    }

    [Fact]
    public async Task Name_type_and_handle_selection_validate_during_mapping()
    {
        WebApplicationBuilder builder = Builder();
        InfoCarrierServerBuilder first = builder.Services.AddInfoCarrierServer<Context>("orders");
        builder.Services.AddInfoCarrierServer<Context>("Orders");
        builder.Services.AddInfoCarrierServer<OtherContext>("other");
        var foreign = new ServiceCollection().AddInfoCarrierServer<Context>("foreign");
        await using WebApplication app = builder.Build();
        Assert.Contains("No default", Assert.Throws<InvalidOperationException>(() => app.MapInfoCarrier()).Message);
        Assert.Contains("ORDERS", Assert.Throws<InvalidOperationException>(() => app.MapInfoCarrier("/missing", serverName: "ORDERS")).Message);
        Assert.Contains("Multiple", Assert.Throws<InvalidOperationException>(() => app.MapInfoCarrier<Context>("/ambiguous")).Message);
        Assert.Contains(nameof(MissingContext), Assert.Throws<InvalidOperationException>(() => app.MapInfoCarrier<MissingContext>("/missing-type")).Message);
        Assert.Contains("host", Assert.Throws<InvalidOperationException>(() => app.MapInfoCarrier(foreign, "/foreign")).Message);
        app.MapInfoCarrier(first, "/handle");
        app.MapInfoCarrier("/name", serverName: "Orders");
        app.MapInfoCarrier<OtherContext>("/typed");
    }

    [Fact]
    public async Task Latest_unnamed_server_remains_default_beside_named_servers()
    {
        WebApplicationBuilder builder = Builder();
        InfoCarrierServerBuilder first = builder.Services.AddInfoCarrierServer<Context>();
        InfoCarrierServerBuilder latest = builder.Services.AddInfoCarrierServer<Context>();
        builder.Services.AddInfoCarrierServer<OtherContext>("named");
        await using WebApplication app = builder.Build();
        Assert.Same(latest.Identity, app.Services.GetRequiredService<InfoCarrierServerRegistrationStore>().Default.Identity);
        app.MapInfoCarrier();
        app.MapInfoCarrier(first, "/first");
        Assert.Contains("Multiple", Assert.Throws<InvalidOperationException>(() => app.MapInfoCarrier<Context>("/ambiguous")).Message);
    }

    [Fact]
    public void Duplicate_server_names_fail_before_configuration_and_client_names_are_independent()
    {
        var services = new ServiceCollection();
        services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://orders"));
        services.AddInfoCarrierServer<Context>("orders");
        bool invoked = false;
        Assert.Throws<InvalidOperationException>(() => services.AddInfoCarrierServer<Context>("orders", _ => invoked = true));
        Assert.False(invoked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Invalid_names_are_rejected(string? name)
        => Assert.ThrowsAny<ArgumentException>(() => new ServiceCollection().AddInfoCarrierServer<Context>(serverName: name!));

    [Fact]
    public async Task Mapping_without_registrations_has_an_actionable_error()
    {
        await using WebApplication app = Builder().Build();
        Assert.Contains("AddInfoCarrierServer", Assert.Throws<InvalidOperationException>(() => app.MapInfoCarrier<Context>()).Message);
    }

    private static WebApplicationBuilder Builder()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        return builder;
    }

    private static IInfoCarrierClient Client(HttpClient http, string path)
        => InfoCarrierClient.Create(http, o => o.WithEndpoint($"http://localhost/{path}").AllowArbitrarySqlExecution(true));

    private static Context ContextFor(IInfoCarrierClient client)
        => new(new DbContextOptionsBuilder<Context>().UseInfoCarrier(client).Options);

    private static async Task Write(IInfoCarrierClient client, string name)
    {
        await using (Context context = ContextFor(client))
        {
            context.Add(new Item { Id = 1, Name = name });
            Assert.Equal(1, await context.SaveChangesAsync());
        }
        await using (Context context = ContextFor(client))
        {
            Item item = await context.Items.SingleAsync();
            Assert.Equal(name, item.Name);
            Assert.Equal(EntityState.Unchanged, context.Entry(item).State);
            item.Name += "-updated";
            Assert.Equal(1, await context.SaveChangesAsync());
        }
    }

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
        public int Disposals { get; private set; }
        public override void Dispose() { Disposals++; base.Dispose(); }
        public override async ValueTask DisposeAsync() { Disposals++; await base.DisposeAsync(); }
    }

    public sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class OtherContext : DbContext;
    private sealed class MissingContext : DbContext;

    private sealed class Host(WebApplication app, string root, ConcurrentBag<Context> contexts) : IAsyncDisposable
    {
        internal WebApplication App { get; } = app;
        internal ConcurrentBag<Context> Contexts { get; } = contexts;

        internal static async Task<Host> Start(bool byHandle)
        {
            string root = Path.Combine(Path.GetTempPath(), $"icc-endpoints-{Guid.NewGuid():N}");
            WebApplicationBuilder builder = Builder();
            var contexts = new ConcurrentBag<Context>();
            foreach (string name in new[] { "admin", "public" })
            {
                string path = root + "-" + name + ".db";
                builder.Services.AddKeyedScoped<Context>(name, (_, _) =>
                {
                    var context = new Context(new DbContextOptionsBuilder<Context>()
                        .UseSqlite($"Data Source={path};Pooling=false").Options);
                    contexts.Add(context);
                    return context;
                });
            }
            InfoCarrierServerBuilder admin = builder.Services.AddInfoCarrierServer<Context>("admin",
                o => o.AllowArbitrarySqlExecution(true).WithMaxRequestBytes(64 * 1024),
                sp => sp.GetRequiredKeyedService<Context>("admin"));
            InfoCarrierServerBuilder publicServer = builder.Services.AddInfoCarrierServer<Context>("public",
                configure: o => o.WithMaxRequestBytes(16 * 1024),
                resolveContext: sp => sp.GetRequiredKeyedService<Context>("public"));
            WebApplication app = builder.Build();
            if (byHandle)
            {
                app.MapInfoCarrier(admin, "/admin");
                app.MapInfoCarrier(publicServer, "/public");
            }
            else
            {
                app.MapInfoCarrier("/admin", serverName: "admin");
                app.MapInfoCarrier("/public", serverName: "public");
            }
            Assert.Empty(contexts); // Mapping must not resolve ordinary contexts from the root provider.
            foreach (string name in new[] { "admin", "public" })
            {
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredKeyedService<Context>(name).Database.EnsureCreatedAsync();
            }
            await app.StartAsync();
            return new Host(app, root, contexts);
        }

        internal async Task Verify(string name)
        {
            await using AsyncServiceScope scope = App.Services.CreateAsyncScope();
            Assert.Equal(name + "-updated", (await scope.ServiceProvider.GetRequiredKeyedService<Context>(name).Items.SingleAsync()).Name);
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            Assert.All(Contexts, context => Assert.Equal(1, context.Disposals));
            foreach (string name in new[] { "admin", "public" })
            {
                string path = root + "-" + name + ".db";
                File.Delete(path);
                File.Delete(path + "-wal");
                File.Delete(path + "-shm");
            }
        }
    }
}
