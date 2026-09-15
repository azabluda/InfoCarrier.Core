// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.DocumentStoreTests.TestUtilities;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
///         <b>STARTING AND STOPPING THE PROCESS IS <see cref="EmbeddedMongo" />'s JOB, AND IT HAS
///         BEEN SINCE 2026-09-14. THIS FIXTURE OWNED A SECOND COPY OF IT AND THAT COPY WAS A BUG.</b>
///         The bookkeeping is "list the `mongod` processes before the start and after it, and the
///         difference is mine", which is only sound while nothing else is starting one. It is
///         serialized by a semaphore for exactly that reason. <b>A semaphore only serializes the
///         callers that share it.</b> When <see cref="EmbeddedMongo" /> was extracted on
///         2026-09-13 the original was left here, so there were two gates and the tier's two
///         fixture families did not exclude each other at all. Their starts interleaved, a
///         fixture's difference set picked up a NEIGHBOUR's server, and disposal then killed a
///         `mongod` whose tests were still running: `An existing connection was forcibly closed by
///         the remote host`, on whichever classes happened to be in flight.
///     </para>
///     <para>
///         This paragraph used to explain why the duplication was safe. It read, until 2026-09-14:
///         <i>"So each fixture records which `mongod` its own start created and kills that process
///         if disposal left it running."</i> Each fixture still does, through the one type that
///         now holds the gate. <b>The reason survives and the copy does not</b>, which is the
///         general rule: one gate cannot be spelt twice.
///     </para>
///     <para>
///         The original reason is unchanged and is worth keeping. <c>MongoDbRunner.Dispose()</c>
///         does not reliably stop <c>mongod</c> (#102): a run of this tier was measured finishing
///         green and leaving SIX live processes behind, and the next run then failed on data a
///         previous run had written. Orphans are not a tidiness problem here, they are the failure.
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
    private EmbeddedMongo _server = null!;

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
        _server = await EmbeddedMongo.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContext<ShopServerContext>(
            o => o.UseMongoDB(_server.ConnectionString, "shop"));
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
        // The spec suite's transport, not a copy of it (2026-09-13). This fixture had its own
        // three-line `WireTransport` doing the same double round trip, written because the harness
        // was inside the spec project and out of reach. It is a project now, and one transport
        // means a wire-format defect surfaces the same way on every tier.
        var transport = new InProcessInfoCarrierTransport(envelopeServer.DispatchAsync, serializer);

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
        await _server.DisposeAsync();
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
