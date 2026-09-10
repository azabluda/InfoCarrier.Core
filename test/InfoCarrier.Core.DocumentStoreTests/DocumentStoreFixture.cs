// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mongo2Go;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     One MongoDB server, owned by one test class, for the life of that class.
/// </summary>
/// <remarks>
///     <para>
///         <b>THE PROCESS IS OWNED BY THE STORE, WHICH IS WHAT MAKES ITS LIFETIME OBVIOUS.</b> A
///         shared server would be cheaper on paper, and it was the first design. It was dropped
///         because xUnit 2.x has no assembly-level fixture: sharing needs a static that is never
///         disposed, which orphans a process when a run is killed, and it reintroduces the class of
///         bug that already cost this repository a nine-test intermittent when a shared SQLite
///         store's disposal raced a live one. Here the class that started the server stops it, and
///         there is nothing to get wrong.
///     </para>
///     <para>
///         <b>The cost of that choice was measured rather than assumed.</b> Starting mongod as a
///         single-node replica set is about 930 ms warm, and it uses about 150 MB, not the 7.6 GB
///         its WiredTiger cache is configured for. xUnit runs test classes in PARALLEL, so N
///         classes start N servers at the same time: the wall clock cost is roughly one startup
///         rather than N, and the memory ceiling is set by core count rather than by how large this
///         tier grows.
///     </para>
///     <para>
///         <b>A REPLICA SET, NOT A STANDALONE, AND THAT IS NOT OPTIONAL.</b> The MongoDB EF
///         provider wraps <c>SaveChanges</c> in a transaction so that a multi-document write
///         applies wholly or not at all, and MongoDB has transactions only on a replica set. A
///         standalone <c>MongoDbRunner.Start()</c> throws on the first save, which is how this was
///         found.
///     </para>
/// </remarks>
public sealed class DocumentStoreFixture : IAsyncLifetime
{
    private MongoDbRunner _runner = null!;
    private ServiceProvider _serverProvider = null!;

    /// <summary>
    ///     Options for a client whose server is this store.
    /// </summary>
    public DbContextOptions<ShopClientContext> ClientOptions { get; private set; } = null!;

    /// <summary>
    ///     Options for a client told that its server is not relational.
    /// </summary>
    public DbContextOptions<ShopClientContext> NonRelationalClientOptions { get; private set; } = null!;

    /// <summary>
    ///     The server this store runs, for the few assertions that are about the store rather than
    ///     about the wire.
    /// </summary>
    public IServiceProvider ServerProvider => _serverProvider;

    public async Task InitializeAsync()
    {
        _runner = MongoDbRunner.Start(singleNodeReplSet: true);

        var services = new ServiceCollection();
        services.AddDbContext<ShopServerContext>(
            o => o.UseMongoDB(_runner.ConnectionString, "shop"));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopServerContext>());
        _serverProvider = services.BuildServiceProvider();

        using (IServiceScope scope = _serverProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopServerContext>();
            await db.Database.EnsureCreatedAsync();
            db.Customers.AddRange(Seed());
            await db.SaveChangesAsync();
        }

        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var envelopeServer = new InfoCarrierEnvelopeServer(
            new InProcessInfoCarrierServer(_serverProvider), serializer);
        var transport = new WireTransport(envelopeServer, serializer);

        ClientOptions = new DbContextOptionsBuilder<ShopClientContext>()
            .UseInfoCarrier(new TransportInfoCarrierClient(transport, serializer))
            .Options;

        NonRelationalClientOptions = new DbContextOptionsBuilder<ShopClientContext>()
            .UseInfoCarrier(
                new TransportInfoCarrierClient(transport, serializer),
                o => o.UseNonRelationalServerStore())
            .Options;
    }

    public async Task DisposeAsync()
    {
        await _serverProvider.DisposeAsync();
        _runner.Dispose();
    }

    /// <summary>A client for a test to use.</summary>
    public ShopClientContext CreateClient() => new(ClientOptions);

    /// <summary>A client told the server is a document store.</summary>
    public ShopClientContext CreateNonRelationalClient() => new(NonRelationalClientOptions);

    /// <summary>
    ///     Three customers, shaped so that nesting is what distinguishes them.
    /// </summary>
    public static Customer[] Seed()
        =>
        [
            new Customer
            {
                Id = "alice",
                Name = "Alice",
                Country = "DE",
                Address = new Address { City = "Berlin", Postcode = "10115" },
                Lines = [new OrderLine { Sku = "book", Quantity = 2 }],
            },
            new Customer
            {
                Id = "bob",
                Name = "Bob",
                Country = "PT",
                Address = new Address { City = "Lisbon", Postcode = "1100" },
                Lines =
                [
                    new OrderLine { Sku = "book", Quantity = 1 },
                    new OrderLine { Sku = "lamp", Quantity = 3 },
                ],
            },
            new Customer
            {
                Id = "carol",
                Name = "Carol",
                Country = "DE",
                Address = new Address { City = "Hamburg", Postcode = "20095" },
                Lines = [],
            },
        ];

    /// <summary>
    ///     Round-trips every envelope through real serialization, so a wire-format failure surfaces
    ///     here exactly as it would over a network.
    /// </summary>
    private sealed class WireTransport(InfoCarrierEnvelopeServer server, IInfoCarrierSerializer serializer)
        : IInfoCarrierTransport
    {
        public async Task<InfoCarrierEnvelope> SendAsync(
            InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
        {
            InfoCarrierEnvelope onTheWire =
                serializer.Deserialize<InfoCarrierEnvelope>(serializer.Serialize(request))!;
            InfoCarrierEnvelope response = await server.DispatchAsync(onTheWire, cancellationToken);
            return serializer.Deserialize<InfoCarrierEnvelope>(serializer.Serialize(response))!;
        }
    }
}
