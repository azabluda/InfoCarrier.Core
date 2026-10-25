// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class NamedAdmissionAndLimitTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Named_clients_enforce_total_streamed_response_limits_independently(bool manyRows)
    {
        await using var host = await Host.Start(manyRows);
        var services = new ServiceCollection();
        Register("small", 2048);
        Register("large", 64 * 1024);
        Register("unlimited", null);
        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await Task.WhenAll(RejectSmall(), Read("large"), Read("unlimited"));

        async Task RejectSmall()
        {
            await using Context small = Remote(provider, "small");
            Exception? failure = await Record.ExceptionAsync(() => small.Items.ToListAsync());
            Assert.NotNull(failure);
            Assert.Contains("MaxResponseBytes", failure.ToString());
        }

        async Task Read(string name)
        {
            await using Context context = Remote(provider, name);
            var rows = await context.Items.ToListAsync();
            Assert.Equal(manyRows ? 32 : 1, rows.Count);
            Assert.All(rows, item => Assert.Equal(manyRows ? 256 : 8192, item.Name.Length));
        }

        void Register(string name, int? limit)
            => services.AddInfoCarrierClient(name, o => o.WithEndpoint("http://localhost/allowed").WithMaxResponseBytes(limit))
                .ConfigurePrimaryHttpMessageHandler(() => host.App.GetTestServer().CreateHandler());
    }

    [Fact]
    public async Task Named_endpoints_enforce_streamed_request_limits_independently()
    {
        await using var host = await Host.Start(false);
        using HttpClient http = host.App.GetTestClient();
        string predicate = new('z', 8 * 1024);
        await using (Context limited = Direct(http, "limited"))
        {
            Exception? failure = await Record.ExceptionAsync(() => limited.Items.Where(item => item.Name == predicate).ToListAsync());
            Assert.NotNull(failure);
            Assert.Contains("MaxRequestBytes", failure.ToString());
        }
        await using Context allowed = Direct(http, "allowed");
        Assert.Empty(await allowed.Items.Where(item => item.Name == predicate).ToListAsync());
    }

    [Fact]
    public async Task Anonymous_shape_admission_is_selected_with_the_endpoint()
    {
        await using var host = await Host.Start(false);
        using HttpClient http = host.App.GetTestClient();
        await using Context allowed = Direct(http, "allowed");
        await using Context denied = Direct(http, "denied");
        var rows = await allowed.Items.Select(item => new { item.Id, item.Name }).ToListAsync();
        Assert.Equal(8192, Assert.Single(rows).Name.Length);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            denied.Items.Select(item => new { item.Id, item.Name }).ToListAsync());
        Assert.Contains("anonymous shape is not registered", failure.Message);
    }

    [Fact]
    public async Task Extra_type_admission_is_selected_with_the_endpoint()
    {
        await using var host = await Host.Start(false);
        using HttpClient http = host.App.GetTestClient();
        await using Context allowed = Direct(http, "allowed");
        await using Context denied = Direct(http, "denied");
        Assert.Equal("checked:" + new string('x', 8192),
            await allowed.Items.Select(item => SharedHelper.Decorate(item.Name)).SingleAsync());
        Exception? failure = await Record.ExceptionAsync(() =>
            denied.Items.Select(item => SharedHelper.Decorate(item.Name)).SingleAsync());
        Assert.NotNull(failure);
        Assert.Contains(nameof(SharedHelper), failure.ToString());
    }

    private static Context Remote(IServiceProvider provider, string name)
        => new(new DbContextOptionsBuilder<Context>().UseApplicationServiceProvider(provider).UseInfoCarrier(name).Options);

    private static Context Direct(HttpClient http, string endpoint)
        => new(new DbContextOptionsBuilder<Context>().UseInfoCarrier(InfoCarrierClient.Create(http, o => o
            .WithEndpoint("http://localhost/" + endpoint)
            .AddAllowedTypes(typeof(SharedHelper))
            .AddAnonymousShapes(new { Id = 0, Name = "" }))).Options);

    public static class SharedHelper
    {
        public static string Decorate(string value) => "checked:" + value;
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

    private sealed class Host(WebApplication app, string path) : IAsyncDisposable
    {
        internal WebApplication App { get; } = app;

        internal static async Task<Host> Start(bool manyRows)
        {
            string path = Path.Combine(Path.GetTempPath(), $"icc-admission-{Guid.NewGuid():N}.db");
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddDbContext<Context>(o => o.UseSqlite($"Data Source={path};Pooling=false"));
            builder.Services.AddInfoCarrierServer<Context>("allowed", o => o.WithMaxRequestBytes(64 * 1024)
                .AddAllowedTypes(typeof(SharedHelper)).AddAnonymousShapes(new { Id = 0, Name = "" }));
            builder.Services.AddInfoCarrierServer<Context>("denied");
            builder.Services.AddInfoCarrierServer<Context>("limited", o => o.WithMaxRequestBytes(1024));
            WebApplication app = builder.Build();
            foreach (string name in new[] { "allowed", "denied", "limited" })
            {
                app.MapInfoCarrier("/" + name, serverName: name);
            }
            using (IServiceScope scope = app.Services.CreateScope())
            {
                Context context = scope.ServiceProvider.GetRequiredService<Context>();
                await context.Database.EnsureCreatedAsync();
                for (int i = 0; i < (manyRows ? 32 : 1); i++)
                {
                    context.Add(new Item { Id = i + 1, Name = new string('x', manyRows ? 256 : 8192) });
                }
                await context.SaveChangesAsync();
            }
            await app.StartAsync();
            return new Host(app, path);
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
