// Licensed under the MIT license. See license.txt file in the project root for license information.

// Inspect EF's actual shared cache and own the explicit pools used by independent same-type factories.
// These internal services let the test verify cache reuse and dispose every retained pooled context.
#pragma warning disable EF1001

using System.Text.Json;
using InfoCarrier.Core.AspNetCore;
using InfoCarrier.Core.ValueMapping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class NamedRegistrationCacheIsolationTest
{
    private const int Iterations = 12;
    // Reuse three deliberate provider configurations across theory cases. Fresh mapper identities
    // per case would create providers unrelated to the isolation question and exhaust EF's warning limit.
    private static readonly PrefixUriMapper OrdersMapper = new("orders:");
    private static readonly PrefixUriMapper ArchiveMapper = new("archive:");
    private static readonly PrefixUriMapper IncomingMapper = new("incoming:");

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Named_clients_and_servers_keep_mappers_and_cached_execution_local(
        bool sharedMapper, bool pooled, bool clientsFirst)
    {
        PrefixUriMapper firstMapper = OrdersMapper;
        PrefixUriMapper secondMapper = sharedMapper ? firstMapper : ArchiveMapper;
        PrefixUriMapper incomingMapper = IncomingMapper;
        var firstServerMapper = new PrefixUriMapper(firstMapper.Prefix);
        var secondServerMapper = new PrefixUriMapper(secondMapper.Prefix);
        Dictionary<PrefixUriMapper, (int Reads, int Writes)> before =
            new[] { firstMapper, secondMapper, incomingMapper, firstServerMapper, secondServerMapper }
                .Distinct().ToDictionary(mapper => mapper, mapper => (mapper.Reads, mapper.Writes));
        await using var upstream = await StoreHost.Start(
            ("orders", firstServerMapper), ("archive", secondServerMapper));
        await using var middle = await StoreHost.Start(
            [("orders", new PrefixUriMapper(incomingMapper.Prefix))],
            services =>
            {
                AddClient(services, "orders", "orders", upstream.App, firstMapper);
                AddClient(services, "archive", "archive", upstream.App, secondMapper);
            }, clientsFirst);
        using HttpClient incomingHttp = middle.App.GetTestClient();
        IInfoCarrierClient incoming = InfoCarrierClient.Create(incomingHttp,
            o => o.WithEndpoint("http://localhost/orders").AddValueMappers(incomingMapper));
        await using var first = new Factory(Options(middle.App.Services, "orders"), pooled);
        await using var second = new Factory(Options(middle.App.Services, "archive"), pooled);
        await using var third = new Factory(new DbContextOptionsBuilder<Context>().UseInfoCarrier(incoming).Options, pooled);

        // Equal mapper instances must really share EF's cache. Different mapper instances must not.
        // Separate caches in every case would conceal the captured-client/serializer defect.
        await using (Context a = first.CreateDbContext())
        await using (Context b = second.CreateDbContext())
        {
            if (sharedMapper)
            {
                Assert.Same(a.GetService<ICompiledQueryCache>(), b.GetService<ICompiledQueryCache>());
                Assert.Same(a.Model, b.Model);
            }
            else
            {
                Assert.NotSame(a.GetService<ICompiledQueryCache>(), b.GetService<ICompiledQueryCache>());
            }
            Assert.NotSame(a.GetService<IExpressionSerializer>(), b.GetService<IExpressionSerializer>());
        }

        Func<Context, int, Task<Item>> firstQuery = CompiledQuery();
        // EF compiled delegates require one model. Share the delegate only in the matching-model case.
        Func<Context, int, Task<Item>> secondQuery = sharedMapper ? firstQuery : CompiledQuery();
        await Task.WhenAll(
            Exercise(first, "orders", pooled, firstQuery),
            Exercise(second, "archive", pooled, secondQuery),
            Exercise(third, "orders", pooled, CompiledQuery()));

        await upstream.Verify("orders");
        await upstream.Verify("archive");
        await middle.Verify("orders");
        foreach ((PrefixUriMapper mapper, (int reads, int writes)) in before)
        {
            Assert.True(mapper.Reads > reads, $"Mapper '{mapper.Prefix}' did not read any values in this case.");
            Assert.True(mapper.Writes > writes, $"Mapper '{mapper.Prefix}' did not write any values in this case.");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_sqlite_control_tracks_and_saves_the_same_workload(bool pooled)
    {
        await using var stores = await StoreHost.Start(
            ("orders", new PrefixUriMapper("unused:")), ("archive", new PrefixUriMapper("unused:")));
        await using var first = new Factory(stores.DirectOptions("orders"), pooled);
        await using var second = new Factory(stores.DirectOptions("archive"), pooled);
        Func<Context, int, Task<Item>> query = CompiledQuery();
        await Task.WhenAll(Exercise(first, "orders", pooled, query), Exercise(second, "archive", pooled, query));
        await stores.Verify("orders");
        await stores.Verify("archive");
    }

    private static void AddClient(
        IServiceCollection services, string name, string endpoint, WebApplication host, PrefixUriMapper mapper)
        => services.AddInfoCarrierClient(name, o => o.WithEndpoint($"http://localhost/{endpoint}").AddValueMappers(mapper))
            .ConfigurePrimaryHttpMessageHandler(() => host.GetTestServer().CreateHandler());

    private static DbContextOptions<Context> Options(IServiceProvider provider, string name)
        => new DbContextOptionsBuilder<Context>().UseApplicationServiceProvider(provider).UseInfoCarrier(name).Options;

    private static Func<Context, int, Task<Item>> CompiledQuery()
        => EF.CompileAsyncQuery((Context context, int id) => context.Items.Single(item => item.Id == id));

    private static async Task Exercise(
        Factory factory, string owner, bool pooled, Func<Context, int, Task<Item>> compiled)
    {
        Context? previous = null;
        for (var revision = 0; revision < Iterations; revision++)
        {
            await using Context context = factory.CreateDbContext();
            if (pooled && previous is not null) { Assert.Same(previous, context); }
            Assert.Empty(context.ChangeTracker.Entries());
            Item item = revision % 2 == 0
                ? await compiled(context, 1)
                : await context.Items.SingleAsync(value => value.Id == 1);
            Assert.Equal(owner, item.Owner);
            Assert.Equal(Address(owner, revision - 1), item.Address);
            Assert.Equal(EntityState.Unchanged, context.Entry(item).State);
            Assert.Single(context.ChangeTracker.Entries());
            item.Revision = revision;
            item.Address = Address(owner, revision);
            Assert.Equal(1, await context.SaveChangesAsync());
            Assert.Equal(EntityState.Unchanged, context.Entry(item).State);
            previous = context;
        }

        await using Context final = factory.CreateDbContext();
        Uri address = Address(owner, Iterations - 1);
        Assert.Equal(address, await final.Items.Where(item => item.Address == address)
            .Select(item => item.Address).SingleAsync());
        // Enumerate the query stream as well as single-result and explicitly compiled query paths.
        await foreach (Item item in final.Items.AsAsyncEnumerable())
        {
            Assert.Equal(owner, item.Owner);
            Assert.Equal(Address(owner, Iterations - 1), item.Address);
        }
        Assert.Single(final.ChangeTracker.Entries());
    }

    private static Uri Address(string owner, int revision) => new($"https://{owner}.example/{revision}");

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Item>().Property(item => item.Address)
                .HasConversion(uri => uri.AbsoluteUri, value => new Uri(value));
    }

    public sealed class Item
    {
        public int Id { get; set; }
        public string Owner { get; set; } = "";
        public Uri Address { get; set; } = null!;
        public int Revision { get; set; }
    }

    private sealed class Factory : IDbContextFactory<Context>, IAsyncDisposable
    {
        private readonly DbContextOptions<Context> _options;
        private readonly DbContextPool<Context>? _pool;
        private readonly PooledDbContextFactory<Context>? _pooledFactory;

        internal Factory(DbContextOptions<Context> options, bool pooled)
        {
            _options = options;
            if (pooled)
            {
                _pool = new DbContextPool<Context>(options);
                _pooledFactory = new PooledDbContextFactory<Context>(_pool);
            }
        }

        public Context CreateDbContext() => _pooledFactory?.CreateDbContext() ?? new Context(_options);
        public ValueTask DisposeAsync() => _pool?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private sealed class PrefixUriMapper(string prefix) : IInfoCarrierValueMapper
    {
        private int _reads;
        private int _writes;
        internal string Prefix { get; } = prefix;
        internal int Reads => Volatile.Read(ref _reads);
        internal int Writes => Volatile.Read(ref _writes);

        public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
        {
            wireValue = null;
            if (value is not Uri uri) { return false; }
            Interlocked.Increment(ref _writes);
            wireValue = Prefix + uri.AbsoluteUri;
            return true;
        }

        public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
        {
            value = null;
            if (declaredType != typeof(Uri)) { return false; }
            string? text = wireValue switch
            {
                string str => str,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                _ => null,
            };
            if (text is null || !text.StartsWith(Prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"A value reached the wrong mapper. Expected prefix '{Prefix}'.");
            }
            Interlocked.Increment(ref _reads);
            value = new Uri(text[Prefix.Length..]);
            return true;
        }
    }

    private sealed class StoreHost(WebApplication app, string root, string[] names) : IAsyncDisposable
    {
        internal WebApplication App { get; } = app;

        internal DbContextOptions<Context> DirectOptions(string name)
            => StoreOptions(root, name);

        private static DbContextOptions<Context> StoreOptions(string root, string name)
            => new DbContextOptionsBuilder<Context>().UseSqlite($"Data Source={root}-{name}.db;Pooling=false").Options;

        internal static Task<StoreHost> Start(params (string Name, PrefixUriMapper Mapper)[] registrations)
            => Start(registrations, null, false);

        internal static async Task<StoreHost> Start(
            (string Name, PrefixUriMapper Mapper)[] registrations,
            Action<IServiceCollection>? addClients, bool clientsFirst)
        {
            string root = Path.Combine(Path.GetTempPath(), $"icc-policy-cache-{Guid.NewGuid():N}");
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            if (clientsFirst) { addClients?.Invoke(builder.Services); }
            foreach ((string name, PrefixUriMapper mapper) in registrations)
            {
                builder.Services.AddKeyedScoped<Context>(name, (_, _) => new Context(StoreOptions(root, name)));
                builder.Services.AddInfoCarrierServer<Context>(name, o => o.AddValueMappers(mapper),
                    sp => sp.GetRequiredKeyedService<Context>(name));
            }
            if (!clientsFirst) { addClients?.Invoke(builder.Services); }
            WebApplication app = builder.Build();
            foreach ((string name, _) in registrations)
            {
                app.MapInfoCarrier("/" + name, serverName: name);
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                Context context = scope.ServiceProvider.GetRequiredKeyedService<Context>(name);
                await context.Database.EnsureCreatedAsync();
                context.Add(new Item { Id = 1, Owner = name, Address = Address(name, -1), Revision = -1 });
                await context.SaveChangesAsync();
            }
            await app.StartAsync();
            return new StoreHost(app, root, registrations.Select(value => value.Name).ToArray());
        }

        internal async Task Verify(string name)
        {
            await using var context = new Context(DirectOptions(name));
            Item item = await context.Items.SingleAsync();
            Assert.Equal(name, item.Owner);
            Assert.Equal(Iterations - 1, item.Revision);
            Assert.Equal(Address(name, Iterations - 1), item.Address);
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            foreach (string name in names)
            {
                string path = root + "-" + name + ".db";
                File.Delete(path);
                File.Delete(path + "-wal");
                File.Delete(path + "-shm");
            }
        }
    }
}
