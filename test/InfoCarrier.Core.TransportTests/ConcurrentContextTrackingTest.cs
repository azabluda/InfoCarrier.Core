// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     Concurrent client contexts must not corrupt one another's change trackers.
/// </summary>
/// <remarks>
///     <para>
///         <b>Self-contained on purpose: no MongoDB, no specification harness, no fixtures.</b> The
///         defect was found through ADR-009 Tier D and looked like a document-store problem for
///         several hours. It is not one. This reproduces it with SQLite behind an ordinary
///         in-process server, which is as close to a plain application as this repository gets.
///     </para>
///     <para>
///         <b>The control is in the same file</b>, because a concurrency finding without one is an
///         accusation rather than a measurement. <see cref="Plain_EF_Core_tracks_correctly" /> runs
///         the identical loop against the SERVER's own contexts, EF Core and SQLite with
///         InfoCarrier out of the picture, and it stays clean.
///     </para>
/// </remarks>
public class ConcurrentContextTrackingTest
{
    private const int Workers = 3;
    private const int Iterations = 200;

    /// <summary>Root, owned reference and owned collection: four tracked entries per query.</summary>
    private const int ExpectedEntries = 4;

    [Fact]
    public async Task Concurrent_client_contexts_track_their_own_entities()
    {
        using Harness harness = await Harness.CreateAsync();

        List<string> wrong = await RunAsync(harness.CreateClient);

        Assert.Empty(wrong);
    }

    /// <summary>
    ///     The control. Same loop, same store, same concurrency, no InfoCarrier.
    /// </summary>
    [Fact]
    public async Task Plain_EF_Core_tracks_correctly()
    {
        using Harness harness = await Harness.CreateAsync();

        List<string> wrong = await RunAsync(harness.CreateServerContext);

        Assert.Empty(wrong);
    }

    private static async Task<List<string>> RunAsync(Func<ShopContext> createContext)
    {
        var wrong = new List<string>();
        var identities = new List<(int Context, int Translator, int Serializer)>();
        var gate = new Lock();

        await Task.WhenAll(Enumerable.Range(0, Workers).Select(worker => Task.Run(async () =>
        {
            for (var i = 0; i < Iterations; i++)
            {
                await using ShopContext context = createContext();

                Shop shop;
                try
                {
                    shop = await context.Shops.SingleAsync(s => s.Id == "one");
                }
                catch (Exception ex)
                {
                    // A THROW IS A SYMPTOM TOO, and collecting it rather than failing here is what
                    // makes this one repro instead of two: the corruption shows up as a wrong
                    // tracker on some iterations and as an exception out of the client pipeline on
                    // others, and the proportions are part of the evidence.
                    lock (gate)
                    {
                        wrong.Add($"worker {worker} iteration {i} THREW {ex.GetType().Name}: {ex.Message}");
                    }

                    continue;
                }

                shop.Name = "changed" + i;
                context.ChangeTracker.DetectChanges();

                int tracked = context.ChangeTracker.Entries().Count();
                if (tracked != ExpectedEntries)
                {
                    string detail = string.Join(
                        " | ",
                        context.ChangeTracker.Entries().Select(
                            e => e.Metadata.ShortName() + ":" + e.State + ":"
                                + string.Join(
                                    ",",
                                    e.Properties.Where(p => p.Metadata.IsKey())
                                        .Select(p => p.Metadata.Name + "=" + p.CurrentValue))));

                    lock (gate)
                    {
                        wrong.Add($"worker {worker} iteration {i} tracked={tracked}: {detail}");
                    }
                }
            }
        })));

        System.IO.File.WriteAllLines(
            System.IO.Path.Combine(AppContext.BaseDirectory, "identities.txt"),
            [
                $"contexts={identities.Select(x => x.Context).Distinct().Count()} "
                + $"translators={identities.Select(x => x.Translator).Distinct().Count()} "
                + $"serializers={identities.Select(x => x.Serializer).Distinct().Count()} "
                + $"samples={identities.Count}",
            ]);

        return wrong;
    }

    private sealed class Harness : IDisposable
    {
        private string _path = null!;
        private ServiceProvider _serverProvider = null!;
        private DbContextOptions<ShopContext> _clientOptions = null!;

        public static async Task<Harness> CreateAsync()
        {
            var harness = new Harness();

            // A FILE, NOT A SHARED IN-MEMORY CONNECTION. One SqliteConnection cannot serve
            // concurrent contexts, which is the harness's problem rather than the product's; a file
            // gives every context its own connection, as an application would have.
            harness._path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"icc-concurrency-{Guid.NewGuid():N}.db");

            var services = new ServiceCollection();
            services.AddDbContext<ShopContext>(o => o.UseSqlite($"Data Source={harness._path};Pooling=false"));
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopContext>());
            harness._serverProvider = services.BuildServiceProvider();

            using (IServiceScope scope = harness._serverProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
                await db.Database.EnsureCreatedAsync();
                db.Shops.Add(new Shop
                {
                    Id = "one",
                    Name = "Original",
                    Address = new Addr { City = "Berlin" },
                    Lines = [new Line { Id = 1, Sku = "book" }, new Line { Id = 2, Sku = "lamp" }],
                });
                await db.SaveChangesAsync();
            }

            var serializer = new SystemTextJsonInfoCarrierSerializer();
            var server = new InfoCarrierEnvelopeServer(
                new InProcessInfoCarrierServer(harness._serverProvider), serializer);

            harness._clientOptions = new DbContextOptionsBuilder<ShopContext>()
                .UseInfoCarrier(new TransportInfoCarrierClient(new Transport(server), serializer))
                .Options;

            return harness;
        }

        public ShopContext CreateClient() => new(_clientOptions);

        /// <summary>A server context, for the control.</summary>
        public ShopContext CreateServerContext()
        {
            IServiceScope scope = _serverProvider.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ShopContext>();
        }

        public void Dispose()
        {
            _serverProvider.Dispose();
            try
            {
                System.IO.File.Delete(_path);
            }
            catch (System.IO.IOException)
            {
                // A test artifact in the temp directory; the sweep at the next run's start gets it.
            }
        }

        private sealed class Transport(InfoCarrierEnvelopeServer server) : IInfoCarrierTransport
        {
            public Task<InfoCarrierEnvelope> SendAsync(
                InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
                => server.DispatchAsync(request, cancellationToken);
        }
    }

    private sealed class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Shop> Shops => Set<Shop>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Shop>().OwnsOne(s => s.Address);
            modelBuilder.Entity<Shop>().OwnsMany(s => s.Lines, b =>
            {
                b.WithOwner().HasForeignKey("ShopId");
                b.Property(l => l.Id).ValueGeneratedNever();
                b.HasKey("ShopId", nameof(Line.Id));
            });
        }
    }

    private sealed class Shop
    {
        public string Id { get; set; } = null!;

        public string Name { get; set; } = null!;

        public Addr Address { get; set; } = null!;

        public List<Line> Lines { get; set; } = [];
    }

    private sealed class Addr
    {
        public string City { get; set; } = null!;
    }

    private sealed class Line
    {
        public int Id { get; set; }

        public string Sku { get; set; } = null!;
    }
}
