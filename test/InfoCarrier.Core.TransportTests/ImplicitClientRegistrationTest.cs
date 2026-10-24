// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.ValueMapping;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Northwind.Shared;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class ImplicitClientRegistrationTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Theory]
    [InlineData("context")]
    [InlineData("factory")]
    [InlineData("pool")]
    [InlineData("pooled-factory")]
    public async Task Standard_registrations_query_with_the_default_client(string registration)
    {
        var services = new ServiceCollection();
        switch (registration)
        {
            case "context": services.AddDbContext<NorthwindContext>(o => o.UseInfoCarrier()); break;
            case "factory": services.AddDbContextFactory<NorthwindContext>(o => o.UseInfoCarrier()); break;
            case "pool": services.AddDbContextPool<NorthwindContext>(o => o.UseInfoCarrier()); break;
            case "pooled-factory": services.AddPooledDbContextFactory<NorthwindContext>(o => o.UseInfoCarrier()); break;
        }

        // Registering the client afterward must work: no client is resolved during registration.
        services.AddInfoCarrierClient(options => options
            .WithEndpoint("http://localhost/infocarrier")
            .ConfigureInfoCarrier()
            .AddAllowedTypes(typeof(DateOnly))
            .WithMaxResponseBytes(16 * 1024 * 1024))
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());

        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        for (var lease = 0; lease < 2; lease++)
        {
            using IServiceScope scope = provider.CreateScope();
            await using NorthwindContext context = registration.Contains("factory", StringComparison.Ordinal)
                ? await scope.ServiceProvider.GetRequiredService<IDbContextFactory<NorthwindContext>>().CreateDbContextAsync()
                : scope.ServiceProvider.GetRequiredService<NorthwindContext>();

            Assert.Same(provider.GetRequiredService<IInfoCarrierClient>(),
                context.GetService<IDbContextOptions>().FindExtension<InfoCarrierOptionsExtension>()!.InfoCarrierClient);
            var rows = await context.Orders.OrderBy(order => order.Id)
                .Select(order => new { order.Id, order.CustomerId }).ToListAsync();
            Assert.Equal(NorthwindSeed.OrderCount, rows.Count);
            Assert.Equal("ALFKI", rows[0].CustomerId);
        }
    }

    [Fact]
    public void Missing_application_provider_has_an_actionable_error()
    {
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            new DbContextOptionsBuilder<MappingContext>().UseInfoCarrier());

        Assert.Contains("UseApplicationServiceProvider", failure.Message);
        Assert.Contains("pass a client explicitly", failure.Message);
    }

    [Fact]
    public void Missing_default_client_fails_when_options_are_resolved()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<MappingContext>(o => o.UseInfoCarrier());
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IDbContextFactory<MappingContext>>());

        Assert.Contains("AddInfoCarrierClient", failure.Message);
    }

    [Fact]
    public void Null_options_builder_is_rejected()
        => Assert.Throws<ArgumentNullException>(() =>
            InfoCarrierDbContextOptionsBuilderExtensions.UseInfoCarrier((DbContextOptionsBuilder)null!));

    [Fact]
    public async Task Same_context_type_keeps_remote_mapper_policy_separate_from_local_sqlite()
    {
        string remotePath = Path.Combine(Path.GetTempPath(), $"icc-implicit-remote-{Guid.NewGuid():N}.db");
        string localPath = Path.Combine(Path.GetTempPath(), $"icc-implicit-local-{Guid.NewGuid():N}.db");
        try
        {
            var serverMapper = new UriMapper();
            var serverServices = new ServiceCollection();
            serverServices.AddLogging();
            serverServices.AddDbContext<MappingContext>(o => o.UseSqlite($"Data Source={remotePath};Pooling=false"));
            serverServices.AddInfoCarrierServer<MappingContext>(o => o.AddValueMappers(serverMapper));
            await using ServiceProvider serverProvider = serverServices.BuildServiceProvider(validateScopes: true);
            using (IServiceScope scope = serverProvider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<MappingContext>().Database.EnsureCreatedAsync();
            }

            var clientMapper = new UriMapper();
            var serializer = new SystemTextJsonInfoCarrierSerializer();
            var server = new InfoCarrierEnvelopeServer(serverProvider.GetRequiredService<IInfoCarrierServer>(), serializer);
            IInfoCarrierClient client = InfoCarrierClient.Configure(
                new TransportInfoCarrierClient(new LocalTransport(server), serializer),
                o => o.AddValueMappers(clientMapper));
            var services = new ServiceCollection();
            services.AddSingleton(client);
            services.AddKeyedSingleton<IDbContextFactory<MappingContext>>("remote", (sp, _) =>
                new MappingFactory(new DbContextOptionsBuilder<MappingContext>()
                    .UseApplicationServiceProvider(sp).UseInfoCarrier().Options));
            services.AddKeyedSingleton<IDbContextFactory<MappingContext>>("local", (_, _) =>
                new MappingFactory(new DbContextOptionsBuilder<MappingContext>()
                    .UseSqlite($"Data Source={localPath};Pooling=false").Options));
            await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
            IDbContextFactory<MappingContext> remoteFactory =
                provider.GetRequiredKeyedService<IDbContextFactory<MappingContext>>("remote");
            IDbContextFactory<MappingContext> localFactory =
                provider.GetRequiredKeyedService<IDbContextFactory<MappingContext>>("local");
            var remoteUri = new Uri("https://remote.example/item");
            await using (MappingContext context = remoteFactory.CreateDbContext())
            {
                context.Add(new MappedItem { Id = 1, Address = remoteUri });
                Assert.Equal(1, await context.SaveChangesAsync());
            }
            await using (MappingContext context = remoteFactory.CreateDbContext())
            {
                Assert.Equal(remoteUri, (await context.Items.SingleAsync()).Address);
                Assert.Equal(remoteUri, await context.Items.Where(item => item.Address == remoteUri)
                    .Select(item => item.Address).SingleAsync());
                Assert.Contains(clientMapper, context.GetService<IEnumerable<IInfoCarrierValueMapper>>());
            }
            await using (MappingContext context = localFactory.CreateDbContext())
            {
                Assert.Null(context.GetService<IDbContextOptions>().FindExtension<InfoCarrierOptionsExtension>());
                await context.Database.EnsureCreatedAsync();
                context.Add(new MappedItem { Id = 1, Address = new Uri("https://local.example/item") });
                Assert.Equal(1, await context.SaveChangesAsync());
                context.ChangeTracker.Clear();
                Assert.Equal("local.example", (await context.Items.SingleAsync()).Address.Host);
            }
            Assert.True(clientMapper.Writes > 0);
            Assert.True(clientMapper.Reads > 0);
            Assert.True(serverMapper.Writes > 0);
            Assert.True(serverMapper.Reads > 0);
            using (IServiceScope scope = serverProvider.CreateScope())
            {
                var stored = await scope.ServiceProvider.GetRequiredService<MappingContext>().Items.SingleAsync();
                Assert.Equal(remoteUri, stored.Address);
            }
        }
        finally
        {
            foreach (string path in new[] { remotePath, localPath })
            {
                File.Delete(path);
                File.Delete(path + "-wal");
                File.Delete(path + "-shm");
            }
        }
    }

    public sealed class MappingContext(DbContextOptions<MappingContext> options) : DbContext(options)
    {
        public DbSet<MappedItem> Items => Set<MappedItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MappedItem>().Property(item => item.Address)
                .HasConversion(uri => uri.AbsoluteUri, value => new Uri(value));
    }

    public sealed class MappedItem
    {
        public int Id { get; set; }
        public Uri Address { get; set; } = null!;
    }

    private sealed class MappingFactory(DbContextOptions<MappingContext> options) : IDbContextFactory<MappingContext>
    {
        public MappingContext CreateDbContext() => new(options);
    }

    private sealed class UriMapper : IInfoCarrierValueMapper
    {
        public int Writes { get; private set; }
        public int Reads { get; private set; }

        public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
        {
            wireValue = null;
            if (value is not Uri uri) { return false; }
            Writes++;
            wireValue = "custom:" + uri.AbsoluteUri;
            return true;
        }

        public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
        {
            value = null;
            if (declaredType != typeof(Uri)) { return false; }
            string? text = wireValue switch
            {
                string valueText => valueText,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                _ => null,
            };
            if (text is null) { return false; }
            if (!text.StartsWith("custom:", StringComparison.Ordinal)) { return false; }
            Reads++;
            value = new Uri(text[7..]);
            return true;
        }
    }

    private sealed class LocalTransport(InfoCarrierEnvelopeServer server) : IInfoCarrierTransport
    {
        public InfoCarrierEnvelope Send(InfoCarrierEnvelope request) => server.Dispatch(request);

        public Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => server.DispatchAsync(request, cancellationToken);
    }
}
