// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using InfoCarrier.Core.AspNetCore;
using InfoCarrier.Core.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Context = InfoCarrier.Core.TransportTests.ClientServerCoexistenceTest.Context;
using Item = InfoCarrier.Core.TransportTests.ClientServerCoexistenceTest.Item;

namespace InfoCarrier.Core.TransportTests;

public class ServiceReplacementTest
{
    [Theory]
    [InlineData("default")]
    [InlineData("name")]
    [InlineData("handle")]
    [InlineData("type")]
    public async Task Custom_serializer_works_for_every_endpoint_selection(string selection)
    {
        var host = await Host.Start(selection, custom: true);
        await using (host)
        {
            var services = new ServiceCollection();
            var observations = new Observations();
            services.AddSingleton(observations);
            services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://localhost/infocarrier"))
                .ConfigurePrimaryHttpMessageHandler(() => host.App.GetTestServer().CreateHandler())
                .ReplaceService<IInfoCarrierSerializer, PrefixSerializer>();
            await using (ServiceProvider provider = services.BuildServiceProvider(validateScopes: true))
            {
                await Write(provider, "orders", "custom");
                await host.Verify("custom-updated");
            }
            Assert.Equal(1, Assert.Single(observations.Serializers).Disposals);
            Assert.True(Assert.Single(observations.Serializers).Reads > 0);
            Assert.True(Assert.Single(host.Observations.Serializers).Reads > 0);
        }
        Assert.Equal(1, Assert.Single(host.Observations.Serializers).Disposals);
    }

    [Fact]
    public async Task Replacement_transport_and_default_client_keep_stored_data_and_dependencies_separate()
    {
        await using var orders = await Host.Start("name", custom: true);
        await using var catalog = await Host.Start("default", custom: false);
        var observations = new Observations();
        var services = new ServiceCollection();
        services.AddSingleton(observations);
        services.AddSingleton(_ => new Target(orders.App.GetTestServer().CreateClient()));
        services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://localhost/infocarrier"))
            .ReplaceService<IInfoCarrierSerializer, PrefixSerializer>()
            .ReplaceService<IInfoCarrierTransport, ReplacementTransport>()
            .ConfigurePrimaryHttpMessageHandler(() => throw new InvalidOperationException("Unused HTTP adapter activated."));
        services.AddInfoCarrierClient("catalog", o => o.WithEndpoint("http://localhost/infocarrier"))
            .ConfigurePrimaryHttpMessageHandler(() => catalog.App.GetTestServer().CreateHandler());
        await using (ServiceProvider provider = services.BuildServiceProvider(validateScopes: true))
        {
            await Task.WhenAll(Write(provider, "orders", "orders"), Write(provider, "catalog", "catalog"));
            await orders.Verify("orders-updated");
            await catalog.Verify("catalog-updated");
            Assert.Single(observations.Serializers);
            ReplacementTransport transport = Assert.Single(observations.Transports);
            IInfoCarrierClient attached = new DbContextOptionsBuilder().UseApplicationServiceProvider(provider)
                .UseInfoCarrier("orders").Options.FindExtension<InfoCarrierOptionsExtension>()!.InfoCarrierClient!;
            InfoCarrierRegistrationIdentity identity = ((IInfoCarrierClientConfiguration)attached).Registration!.Identity;
            Assert.Same(provider.GetRequiredKeyedService<IInfoCarrierSerializer>(identity), transport.Serializer);
            Assert.Same(observations, transport.Observations);
            Assert.True(transport.Sends > 0);
        }
        Assert.Equal(1, Assert.Single(observations.Transports).Disposals);
        Assert.Equal(1, Assert.Single(observations.Serializers).Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Specific_original_implementation_replacement_takes_precedence(bool specificFirst)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Observations>();
        InfoCarrierClientBuilder builder = services.AddInfoCarrierClient(o => o.WithEndpoint("http://localhost"));
        if (specificFirst) { Specific(); }
        builder.ReplaceService<IInfoCarrierSerializer, UnusedSerializer>();
        if (!specificFirst) { Specific(); }
        // Matches original descriptors, not the result of an earlier substitution.
        builder.ReplaceService<IInfoCarrierSerializer, PrefixSerializer, UnusedSerializer>();
        using ServiceProvider provider = services.BuildServiceProvider();
        _ = new DbContextOptionsBuilder().UseApplicationServiceProvider(provider).UseInfoCarrier();
        Assert.IsType<PrefixSerializer>(Assert.Single(provider.GetRequiredService<Observations>().Serializers));

        void Specific() => builder.ReplaceService<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer, PrefixSerializer>();
    }

    [Fact]
    public void Repeated_contract_replacement_uses_last_choice_and_does_not_construct_old_choice()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Observations>();
        services.AddInfoCarrierClient(o => o.WithEndpoint("http://localhost"))
            .ReplaceService<IInfoCarrierSerializer, UnusedSerializer>()
            .ReplaceService<IInfoCarrierSerializer, PrefixSerializer>();
        using ServiceProvider provider = services.BuildServiceProvider();
        _ = new DbContextOptionsBuilder().UseApplicationServiceProvider(provider).UseInfoCarrier();
        Assert.IsType<PrefixSerializer>(Assert.Single(provider.GetRequiredService<Observations>().Serializers));
    }

    [Fact]
    public async Task Failed_transport_construction_leaves_serializer_owned_by_host()
    {
        var services = new ServiceCollection();
        var observations = new Observations();
        services.AddSingleton(observations);
        services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://localhost"))
            .ReplaceService<IInfoCarrierSerializer, PrefixSerializer>()
            .ReplaceService<IInfoCarrierTransport, FailingTransport>();
        await using (ServiceProvider provider = services.BuildServiceProvider(validateScopes: true))
        {
            Assert.Throws<ArgumentException>(() => new DbContextOptionsBuilder()
                .UseApplicationServiceProvider(provider).UseInfoCarrier("orders"));
            Assert.Equal(0, Assert.Single(observations.Serializers).Disposals);
        }
        Assert.Equal(1, Assert.Single(observations.Serializers).Disposals);
    }

    [Fact]
    public void Replacement_cannot_capture_a_scoped_host_dependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Observations>();
        services.AddScoped<ScopedDependency>();
        services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://localhost"))
            .ReplaceService<IInfoCarrierSerializer, ScopedSerializer>();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => new DbContextOptionsBuilder()
            .UseApplicationServiceProvider(provider).UseInfoCarrier("orders"));
        Assert.Contains("scoped", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Existing_provider_and_snapshot_keep_their_original_component_choices()
    {
        var services = new ServiceCollection();
        var observations = new Observations();
        services.AddSingleton(observations);
        InfoCarrierClientBuilder builder = services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://localhost"));
        using ServiceProvider first = services.BuildServiceProvider();
        _ = new DbContextOptionsBuilder().UseApplicationServiceProvider(first).UseInfoCarrier("orders");
        InfoCarrierClientRegistrationStore snapshot = first.GetRequiredService<InfoCarrierClientRegistrationStore>();
        builder.ReplaceService<IInfoCarrierSerializer, PrefixSerializer>();
        _ = new DbContextOptionsBuilder().UseApplicationServiceProvider(first).UseInfoCarrier("orders");
        Assert.Empty(observations.Serializers);
        using ServiceProvider second = services.BuildServiceProvider();
        _ = new DbContextOptionsBuilder().UseApplicationServiceProvider(second).UseInfoCarrier("orders");
        Assert.Single(observations.Serializers);
        Assert.NotSame(snapshot, second.GetRequiredService<InfoCarrierClientRegistrationStore>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void One_implementation_type_can_replace_two_contracts_with_independent_owned_instances(bool serializerFirst)
    {
        var services = new ServiceCollection();
        var observations = new Observations();
        services.AddSingleton(observations);
        InfoCarrierClientBuilder builder = services.AddInfoCarrierClient(o => o.WithEndpoint("http://localhost"));
        if (serializerFirst) { Serializer(); }
        builder.ReplaceService<IInfoCarrierTransport, CombinedImplementation>();
        if (!serializerFirst) { Serializer(); }
        using (ServiceProvider provider = services.BuildServiceProvider())
        {
            _ = new DbContextOptionsBuilder().UseApplicationServiceProvider(provider).UseInfoCarrier();
            PrefixSerializer[] instances = observations.Serializers.ToArray();
            Assert.Equal(2, instances.Length);
            Assert.NotSame(instances[0], instances[1]);
        }
        Assert.All(observations.Serializers, serializer => Assert.Equal(1, serializer.Disposals));

        void Serializer() => builder.ReplaceService<IInfoCarrierSerializer, CombinedImplementation>();
    }

    [Fact]
    public async Task Configured_limits_apply_before_custom_payload_decoding()
    {
        var observations = new Observations();
        var inner = new PrefixSerializer(observations);
        var serializer = new InfoCarrierSerializerBoundary(inner, new InfoCarrierPayloadLimits(8, 8));
        Assert.Throws<InvalidOperationException>(() => serializer.Deserialize<QueryDataRequest>(new byte[9]));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await serializer.DeserializeAsync<QueryDataResult>(new byte[9]));
        Assert.Equal(0, inner.Reads);
        using var stream = new MemoryStream(new byte[9]);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await serializer.DeserializeAsync<QueryDataRequest>(stream));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Custom_serializer_cannot_bypass_total_streamed_response_limit()
    {
        await using var host = await Host.Start("name", custom: true);
        using (IServiceScope scope = host.App.Services.CreateScope())
        {
            Context db = scope.ServiceProvider.GetRequiredService<Context>();
            db.Add(new Item { Id = 1, Name = new string('x', 2000) });
            await db.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddSingleton<Observations>();
        services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://localhost/infocarrier").WithMaxResponseBytes(300))
            .ReplaceService<IInfoCarrierSerializer, PrefixSerializer>()
            .ConfigurePrimaryHttpMessageHandler(() => host.App.GetTestServer().CreateHandler());
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using var context = new Context(new DbContextOptionsBuilder<Context>()
            .UseApplicationServiceProvider(provider).UseInfoCarrier("orders").Options);
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Items.ToListAsync());
        Assert.Contains("MaxResponseBytes", failure.Message);
    }

    [Fact]
    public void Unsupported_contract_and_non_InfoCarrier_builder_fail_during_configuration()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddInfoCarrierClient(o => o.WithEndpoint("http://localhost"))
            .ReplaceService<Observations, Observations>());
        Assert.Throws<InvalidOperationException>(() => services.AddHttpClient("application")
            .ReplaceService<IInfoCarrierSerializer, PrefixSerializer>());
        Assert.Throws<InvalidOperationException>(() => services.AddInfoCarrierServer<Context>()
            .ReplaceService<IInfoCarrierTransport, ReplacementTransport>());
    }

    private static async Task Write(IServiceProvider provider, string name, string value)
    {
        DbContextOptions<Context> options = new DbContextOptionsBuilder<Context>()
            .UseApplicationServiceProvider(provider).UseInfoCarrier(name).Options;
        await using (var db = new Context(options))
        {
            db.Add(new Item { Id = 1, Name = value });
            Assert.Equal(1, await db.SaveChangesAsync());
        }
        await using (var db = new Context(options))
        {
            Item item = await db.Items.SingleAsync();
            Assert.Equal(value, item.Name);
            Assert.Equal(EntityState.Unchanged, db.Entry(item).State);
            item.Name += "-updated";
            Assert.Equal(1, await db.SaveChangesAsync());
        }
    }

    public sealed class Observations
    {
        internal ConcurrentBag<PrefixSerializer> Serializers { get; } = [];
        internal ConcurrentBag<ReplacementTransport> Transports { get; } = [];
    }

    public class PrefixSerializer : IInfoCarrierSerializer, IDisposable
    {
        private readonly SystemTextJsonInfoCarrierSerializer _inner = new(new InfoCarrierPayloadLimits(null, null));
        private int _reads;
        public PrefixSerializer(Observations observations) => observations.Serializers.Add(this);
        internal int Reads => _reads;
        internal int Disposals { get; private set; }
        public byte[] Serialize<T>(T value) => [127, .. _inner.Serialize(value)];
        public T? Deserialize<T>(byte[] payload)
        {
            Interlocked.Increment(ref _reads);
            Assert.Equal(127, payload[0]);
            return _inner.Deserialize<T>(payload[1..]);
        }
        public ValueTask<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Serialize(value));
        public ValueTask<T?> DeserializeAsync<T>(byte[] payload, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Deserialize<T>(payload));
        public void Dispose() => Disposals++;
    }

    public sealed class UnusedSerializer(Observations observations) : PrefixSerializer(observations);

    public sealed class ScopedDependency;

    public sealed class CombinedImplementation(Observations observations) : PrefixSerializer(observations), IInfoCarrierTransport
    {
        public InfoCarrierEnvelope Send(InfoCarrierEnvelope request) => request;
        public Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => Task.FromResult(request);
    }

    public sealed class ScopedSerializer(ScopedDependency dependency, Observations observations) : PrefixSerializer(observations)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }

    public sealed class Target(HttpClient client) : IDisposable
    {
        internal HttpClient Client { get; } = client;
        public void Dispose() => Client.Dispose();
    }

    public sealed class ReplacementTransport : IInfoCarrierTransport, IAsyncDisposable
    {
        private readonly HttpInfoCarrierTransport _inner;
        private int _sends;
        public ReplacementTransport([FromKeyedServices] IInfoCarrierSerializer serializer,
            Target target, Observations observations)
        {
            Serializer = serializer;
            Observations = observations;
            observations.Transports.Add(this);
            _inner = new HttpInfoCarrierTransport(target.Client, serializer);
        }
        internal IInfoCarrierSerializer Serializer { get; }
        internal Observations Observations { get; }
        internal int Sends => _sends;
        internal int Disposals { get; private set; }
        public InfoCarrierEnvelope Send(InfoCarrierEnvelope request)
        {
            Interlocked.Increment(ref _sends);
            return _inner.Send(request);
        }
        public Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _sends);
            return _inner.SendAsync(request, cancellationToken);
        }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }

    public sealed class FailingTransport : IInfoCarrierTransport
    {
        public FailingTransport([FromKeyedServices] IInfoCarrierSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            throw new ArgumentException("Controlled transport construction failure.");
        }
        public InfoCarrierEnvelope Send(InfoCarrierEnvelope request) => throw new NotSupportedException();
        public Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class Host(WebApplication app, string path, ConcurrentBag<Task> requests, Observations observations) : IAsyncDisposable
    {
        internal WebApplication App { get; } = app;
        internal Observations Observations { get; } = observations;
        internal static async Task<Host> Start(string selection, bool custom)
        {
            string path = Path.Combine(Path.GetTempPath(), $"icc-replacement-{Guid.NewGuid():N}.db");
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            var observations = new Observations();
            builder.Services.AddSingleton(observations);
            builder.Services.AddDbContext<Context>(o => o.UseSqlite($"Data Source={path};Pooling=false"));
            InfoCarrierServerBuilder server = selection == "default"
                ? builder.Services.AddInfoCarrierServer<Context>()
                : builder.Services.AddInfoCarrierServer<Context>("orders");
            if (custom)
            {
                if (selection == "type")
                {
                    server.ReplaceService<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer, PrefixSerializer>();
                }
                else { server.ReplaceService<IInfoCarrierSerializer, PrefixSerializer>(); }
            }
            WebApplication app = builder.Build();
            var requests = new ConcurrentBag<Task>();
            app.Use(async (_, next) =>
            {
                var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                requests.Add(completed.Task);
                try { await next(); }
                finally { completed.TrySetResult(); }
            });
            switch (selection)
            {
                case "default": app.MapInfoCarrier(); break;
                case "name": app.MapInfoCarrier("infocarrier", serverName: "orders"); break;
                case "handle": app.MapInfoCarrier(server); break;
                case "type": app.MapInfoCarrier<Context>(); break;
            }
            using (IServiceScope scope = app.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<Context>().Database.EnsureCreatedAsync();
            }
            await app.StartAsync();
            return new Host(app, path, requests, observations);
        }
        internal async Task Verify(string value)
        {
            using IServiceScope scope = App.Services.CreateScope();
            Assert.Equal(value, (await scope.ServiceProvider.GetRequiredService<Context>().Items.SingleAsync()).Name);
        }
        public async ValueTask DisposeAsync()
        {
            await App.StopAsync();
            await Task.WhenAll(requests.ToArray()).WaitAsync(TimeSpan.FromSeconds(15));
            await App.DisposeAsync();
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }
}
