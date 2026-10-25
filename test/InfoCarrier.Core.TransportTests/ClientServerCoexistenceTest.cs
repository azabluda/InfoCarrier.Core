// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class ClientServerCoexistenceTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Incoming_and_outgoing_operations_keep_independent_stored_data(bool clientFirst)
    {
        string middlePath = Path.Combine(Path.GetTempPath(), $"icc-middle-{Guid.NewGuid():N}.db");
        string downstreamPath = Path.Combine(Path.GetTempPath(), $"icc-downstream-{Guid.NewGuid():N}.db");
        try
        {
            await using WebApplication downstream = await StartHost(downstreamPath);
            await using WebApplication middle = await StartHost(middlePath, downstream, clientFirst);
            using HttpClient incomingHttp = middle.GetTestClient();
            IInfoCarrierClient incomingClient = InfoCarrierClient.Create(incomingHttp,
                o => o.WithEndpoint("http://localhost/infocarrier"));

            // Both clients execute identical query shapes against the same context type. The middle
            // host owns both its incoming server and its outgoing default client, in either order.
            Task incoming = Exercise(new DbContextOptionsBuilder<Context>().UseInfoCarrier(incomingClient).Options, "middle");
            Task outgoing = Exercise(new DbContextOptionsBuilder<Context>()
                .UseApplicationServiceProvider(middle.Services).UseInfoCarrier().Options, "downstream");
            await Task.WhenAll(incoming, outgoing);

            await VerifyStored(middle.Services, "middle");
            await VerifyStored(downstream.Services, "downstream");
            Assert.Equal(1024 * 1024,
                middle.Services.GetRequiredService<InfoCarrierServerRegistrationStore>().Default.MaxRequestBytes);
            Assert.Equal(2 * 1024 * 1024,
                middle.Services.GetRequiredService<InfoCarrierClientRegistration>().MaxResponseBytes);
        }
        finally
        {
            foreach (string path in new[] { middlePath, downstreamPath })
            {
                File.Delete(path);
                File.Delete(path + "-wal");
                File.Delete(path + "-shm");
            }
        }
    }

    private static async Task<WebApplication> StartHost(
        string path, WebApplication? downstream = null, bool clientFirst = false)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<Context>(o => o.UseSqlite($"Data Source={path};Pooling=false"));
        if (clientFirst) { RegisterClient(); }
        builder.Services.AddInfoCarrierServer<Context>(o => o.WithMaxRequestBytes(1024 * 1024));
        if (!clientFirst) { RegisterClient(); }
        WebApplication app = builder.Build();
        app.MapInfoCarrier();
        using (IServiceScope scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<Context>().Database.EnsureCreatedAsync();
        }
        await app.StartAsync();
        return app;

        void RegisterClient()
        {
            if (downstream is null) { return; }
            builder.Services.AddInfoCarrierClient(o => o.WithEndpoint("http://localhost/infocarrier")
                .WithMaxResponseBytes(2 * 1024 * 1024).AllowArbitrarySqlExecution(true))
                .ConfigurePrimaryHttpMessageHandler(() => downstream.GetTestServer().CreateHandler());
        }
    }

    private static async Task Exercise(DbContextOptions<Context> options, string name)
    {
        await using (var context = new Context(options))
        {
            context.Items.Add(new Item { Id = 1, Name = name });
            Assert.Equal(1, await context.SaveChangesAsync());
            Assert.Equal(EntityState.Unchanged, Assert.Single(context.ChangeTracker.Entries()).State);
        }
        await using (var context = new Context(options))
        {
            Item item = await context.Items.SingleAsync();
            Assert.Equal(name, item.Name);
            Assert.Equal(EntityState.Unchanged, context.Entry(item).State);
            item.Name += "-updated";
            Assert.Equal(1, await context.SaveChangesAsync());
        }
    }

    private static async Task VerifyStored(IServiceProvider provider, string name)
    {
        using IServiceScope scope = provider.CreateScope();
        Context context = scope.ServiceProvider.GetRequiredService<Context>();
        Assert.Equal(name + "-updated", (await context.Items.SingleAsync()).Name);
    }

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
    }

    public sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
