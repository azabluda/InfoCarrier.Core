// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
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
///         <b>A SHARED SERVER WITH A DATABASE PER CLASS WAS TRIED ON 2026-09-11 AND IS WRONG, WHICH
///         IS WORTH RECORDING BECAUSE IT LOOKS RIGHT.</b> It is cheaper on paper for the same reason
///         it was the first design, and it makes isolation a name this repository controls rather
///         than a process it does not. It does not isolate. With six fixtures on one server, each
///         with its own uniquely named database, two test classes still saw each other's rows:
///         moving one class off the customer another class was writing changed a deterministic
///         two-test failure into a green run. Per-class SERVERS do isolate, measured over many runs.
///         <b>So the database name is not a substitute for the process here</b>, and the shape below
///         is the one that works.
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
///         <b>STARTING IS SERIALIZED AND STOPPING IS ENFORCED, BECAUSE `MongoDbRunner.Dispose()`
///         DOES NOT RELIABLY STOP `mongod` (#102).</b> A run of this tier was measured finishing
///         green and leaving SIX live `mongod` processes behind; the next run then failed on data a
///         previous run had written, which is how the tier became intermittent the moment it grew
///         from four classes to six. Orphans are not a tidiness problem here, they are the failure.
///     </para>
///     <para>
///         So each fixture records which `mongod` its own start created and kills that process if
///         disposal left it running. <b>Identifying it is the whole reason the start is
///         serialized</b>: with parallel starts the before-and-after difference is ambiguous and a
///         fixture could claim a neighbour's server. Serializing costs one startup per class in
///         sequence rather than in parallel, and that is the price of a tier whose runs do not
///         poison each other.
///     </para>
///     <para>
///         <b>A REPLICA SET, NOT A STANDALONE, AND THAT IS NOT OPTIONAL.</b> The MongoDB EF
///         provider wraps <c>SaveChanges</c> in a transaction so that a multi-document write
///         applies wholly or not at all, and MongoDB has transactions only on a replica set. A
///         standalone <c>MongoDbRunner.Start()</c> throws on the first save, which is how this was
///         found.
///     </para>
/// </remarks>
public class DocumentStoreFixture : IAsyncLifetime
{
    /// <summary>
    ///     Serializes starting a server, so that "which <c>mongod</c> is mine" has an answer.
    /// </summary>
    private static readonly SemaphoreSlim StartGate = new(1, 1);

    private MongoDbRunner _runner = null!;

    private Process[] _mongod = [];

    private ServiceProvider _serverProvider = null!;

    /// <summary>
    ///     Whether the server says what kind of store it is (#102).
    /// </summary>
    /// <remarks>
    ///     True here because a MongoDB server should say it, and every test in this tier therefore
    ///     runs with the server half of #100 switched on.
    ///     <see cref="UndeclaredDocumentStoreFixture" /> is the one that does not, so the client
    ///     half can be shown to stand on its own.
    /// </remarks>
    protected virtual bool ServerDeclaresDocumentStore => true;

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
        await StartServerAsync();

        var services = new ServiceCollection();
        services.AddDbContext<ShopServerContext>(
            o => o.UseMongoDB(_runner.ConnectionString, "shop"));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopServerContext>());

        if (ServerDeclaresDocumentStore)
        {
            services.AddInfoCarrierServerDocumentStore();
        }

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

        // What `Dispose` was supposed to do. Anything still alive here is an orphan that will hold
        // its port and its data into the next run, which is the failure this guards (#102).
        foreach (Process process in _mongod)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10_000);
                }
            }
            catch (Exception)
            {
                // Already gone, or not ours to kill. Either way there is nothing left to do, and a
                // fixture that throws while tearing down hides the result of the tests it ran.
            }

            process.Dispose();
        }
    }

    /// <summary>
    ///     Starts this fixture's server and records which <c>mongod</c> it created.
    /// </summary>
    /// <remarks>
    ///     Serialized, because the only portable way to name the process a library started is to
    ///     compare the set of them before and after, and that difference means nothing while
    ///     another thread is starting one too. <see cref="Process.GetProcessesByName(string)" />
    ///     answers on Windows, Linux and macOS alike, which the tier's no-installation bar requires.
    /// </remarks>
    private async Task StartServerAsync()
    {
        await StartGate.WaitAsync();
        try
        {
            HashSet<int> before = [.. Process.GetProcessesByName("mongod").Select(p => p.Id)];
            _runner = MongoDbRunner.Start(singleNodeReplSet: true);
            _mongod = [.. Process.GetProcessesByName("mongod").Where(p => !before.Contains(p.Id))];
        }
        finally
        {
            StartGate.Release();
        }
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

/// <summary>
///     A server that has NOT been told what kind of store it is (#102).
/// </summary>
/// <remarks>
///     <b>It exists to show that the client half of #100 still stands on its own.</b> The server
///     half repairs a change set the client left incomplete, and a repair that also happened to
///     hide a regression in the client would be worse than no repair at all. Here nothing repairs
///     anything, so a client that says <c>UseNonRelationalServerStore()</c> is on its own exactly
///     as it was before this existed.
/// </remarks>
public sealed class UndeclaredDocumentStoreFixture : DocumentStoreFixture
{
    protected override bool ServerDeclaresDocumentStore => false;
}
