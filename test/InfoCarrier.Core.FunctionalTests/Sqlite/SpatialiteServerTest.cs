// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using InfoCarrier.Core.ValueMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     Spatial values and spatial queries against a SQL store, SQLite with SpatiaLite, which is
///     what an application's spatial server runs and what ADR-009 Tier A cannot show.
/// </summary>
/// <remarks>
///     <para>
///         <b>Windows only</b>, through <see cref="SpatialiteRequiredAttribute" />, and skipped in CI:
///         the owner kept the spatial spec bases on Tier A on 2026-09-27, and these are the promises
///         a SpatiaLite run found when they were tried on Tier B the same day.
///     </para>
///     <para>
///         Each test compares with plain EF Core on the same store, or states what it would do.
///         The client is built as <c>website/docs/configuration/value-mappers.md</c> describes: its
///         own service provider, with the geometry mapper the test utilities carry.
///     </para>
/// </remarks>
public class SpatialiteServerTest
{
    private static readonly IServiceProvider ClientServices = new ServiceCollection()
        .AddEntityFrameworkInfoCarrier()
        .AddSingleton<IInfoCarrierValueMapper, InfoCarrierNetTopologySuiteValueMapper>()
        .BuildServiceProvider();

    private readonly List<string> _sink = [];

    /// <summary>
    ///     A geometry the server reads from SpatiaLite reaches the client as the same geometry.
    /// </summary>
    /// <remarks>
    ///     Until 2026-09-27 the client failed with <c>JsonException: The JSON value could not be
    ///     converted to NetTopologySuite.Geometries.Geometry</c>. The server wrote the value in the
    ///     JSON form of SQLite's own spatial type mapping; the client's mapping has none, so it
    ///     read the string as JSON for <see cref="Geometry" />. On an InMemory server neither side
    ///     has a JSON form and the geometry mapper carries the value, which is why Tier A never
    ///     showed it. Found as EF's <c>SpatialQueryTestBase.SimpleSelect</c> on SpatiaLite.
    /// </remarks>
    [ConditionalFact, SpatialiteRequired]
    public async Task A_geometry_read_from_a_spatial_store_crosses_the_wire()
    {
        await using SqliteInfoCarrierBackendTestStore store = await CreateSeededStoreAsync(registerAggregate: false);

        await using SpatialContext client = CreateClient(store, registerAggregate: false);
        Site site = await client.Sites.SingleAsync(s => s.Id == 1);

        Assert.True(new Point(1, 2).EqualsExact(site.Location), $"read back {site.Location}");
    }

    /// <summary>
    ///     A geometry the client saves reaches SpatiaLite as the same geometry.
    /// </summary>
    /// <remarks>The write direction of the test above, through the same type mapping on the server.</remarks>
    [ConditionalFact, SpatialiteRequired]
    public async Task A_geometry_saved_over_the_wire_reaches_a_spatial_store()
    {
        await using SqliteInfoCarrierBackendTestStore store = await CreateSeededStoreAsync(registerAggregate: false);

        await using (SpatialContext client = CreateClient(store, registerAggregate: false))
        {
            client.Sites.Add(new Site { Id = 10, Group = "c", Location = new Point(5, 6) });
            await client.SaveChangesAsync();
        }

        await using DbContext server = store.CreateDbContext();
        Site stored = await server.Set<Site>().SingleAsync(s => s.Id == 10);
        Assert.True(new Point(5, 6).EqualsExact(stored.Location), $"stored {stored.Location}");
    }

    /// <summary>
    ///     A spatial aggregate over a grouping runs at the store, as plain EF Core runs it, when the
    ///     application names the aggregate's class on both halves.
    /// </summary>
    /// <remarks>
    ///     <c>UnaryUnionOp.Union(g.Select(s =&gt; s.Location))</c> names a NetTopologySuite class the
    ///     model does not imply, so it is registered as any such type is: <c>AllowTypes</c> on the
    ///     client and <c>AddInfoCarrierAllowedTypes</c> on the server. Plain EF writes
    ///     <c>GUnion("Location")</c> with the <c>GROUP BY</c>. The configuration this suite tests,
    ///     as the owner asked on 2026-09-27; the next test pins the other one.
    /// </remarks>
    [ConditionalFact, SpatialiteRequired]
    public async Task A_spatial_aggregate_registered_on_both_halves_runs_at_the_store()
    {
        (string overTheWire, string directly) = await AggregateStatementBothWaysAsync(registerAggregate: true);

        Assert.Contains("GUnion(", directly, StringComparison.Ordinal);
        Assert.Equal(directly, overTheWire);
    }

    /// <summary>
    ///     An application MISCONFIGURED, with the aggregate's class not registered: the grouping
    ///     and the aggregate run on the client, over every row.
    /// </summary>
    /// <remarks>
    ///     Pinned for documentation, as the owner asked on 2026-09-27: this is what a reader who
    ///     leaves the registration out observes. The answer is the same; the server sends every
    ///     point instead of one union per group. Found as EF's four spatial <c>*_aggregate</c> tests
    ///     on SpatiaLite.
    /// </remarks>
    [ConditionalFact, SpatialiteRequired]
    public async Task A_spatial_aggregate_not_registered_runs_on_the_client()
    {
        (string overTheWire, string directly) = await AggregateStatementBothWaysAsync(registerAggregate: false);

        Assert.Contains("GUnion(", directly, StringComparison.Ordinal);
        Assert.DoesNotContain("GUnion(", overTheWire, StringComparison.Ordinal);
    }

    private async Task<(string OverTheWire, string Directly)> AggregateStatementBothWaysAsync(bool registerAggregate)
    {
        await using SqliteInfoCarrierBackendTestStore store = await CreateSeededStoreAsync(registerAggregate);

        Drain();
        await using (SpatialContext client = CreateClient(store, registerAggregate))
        {
            _ = await client.Sites
                .GroupBy(s => s.Group)
                .Select(g => new { g.Key, Union = UnaryUnionOp.Union(g.Select(s => s.Location)) })
                .ToListAsync();
        }

        string overTheWire = Statement(Drain());

        await using (DbContext server = store.CreateDbContext())
        {
            _ = await server.Set<Site>()
                .GroupBy(s => s.Group)
                .Select(g => new { g.Key, Union = UnaryUnionOp.Union(g.Select(s => s.Location)) })
                .ToListAsync();
        }

        return (overTheWire, Statement(Drain()));
    }

    private async Task<SqliteInfoCarrierBackendTestStore> CreateSeededStoreAsync(bool registerAggregate)
    {
        var store = new SqliteInfoCarrierBackendTestStore(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SpatialContext),
                OnModelCreating = (_, _) => { },
                AllowedTypes = registerAggregate ? [typeof(UnaryUnionOp)] : null,
                OnAddServices = s => s.AddEntityFrameworkSqliteNetTopologySuite(),
                OnAddOptions = b =>
                {
                    new SqliteDbContextOptionsBuilder(b).UseNetTopologySuite();
                    return b.LogTo(
                        line => { lock (_sink) { _sink.Add(line); } },
                        [RelationalEventId.CommandExecuted]);
                },
            });

        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(
                    new Site { Id = 1, Group = "a", Location = new Point(1, 2) },
                    new Site { Id = 2, Group = "a", Location = new Point(3, 4) },
                    new Site { Id = 3, Group = "b", Location = new Point(7, 8) });
                await context.SaveChangesAsync();
            });

        return store;
    }

    private static SpatialContext CreateClient(SqliteInfoCarrierBackendTestStore store, bool registerAggregate)
        => new(
            new DbContextOptionsBuilder<SpatialContext>()
                .UseInternalServiceProvider(ClientServices)
                .UseInfoCarrier(
                    store,
                    o =>
                    {
                        if (registerAggregate)
                        {
                            o.AllowTypes(typeof(UnaryUnionOp));
                        }
                    })
                .Options);

    private string[] Drain()
    {
        lock (_sink)
        {
            string[] copy = [.. _sink];
            _sink.Clear();
            return copy;
        }
    }

    /// <summary>The one statement in <paramref name="logged" />, made positional by <see cref="SqlNormalizer" />.</summary>
    private static string Statement(string[] logged)
    {
        string entry = Assert.Single(logged);
        string[] lines = entry.Split('\n');
        int start = Array.FindIndex(lines, l => l.TrimStart().StartsWith("SELECT", StringComparison.Ordinal));
        Assert.True(start >= 0, "no SELECT found in: " + entry);
        return SqlNormalizer.Normalize(string.Join('\n', lines[start..].Select(l => l.Trim())));
    }

    /// <summary>A site with a location, grouped by <see cref="Group" />.</summary>
    public class Site
    {
        public int Id { get; set; }

        public string Group { get; set; } = "";

        public Point? Location { get; set; }
    }

    /// <summary>The model both halves build.</summary>
    public class SpatialContext(DbContextOptions<SpatialContext> options) : DbContext(options)
    {
        public DbSet<Site> Sites => Set<Site>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Site>().Property(s => s.Id).ValueGeneratedNever();
    }
}
