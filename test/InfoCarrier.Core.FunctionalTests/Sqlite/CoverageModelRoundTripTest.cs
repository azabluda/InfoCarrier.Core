// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

public class CoverageModelRoundTripTest
{
    private static SqliteInfoCarrierBackendTestStore CreateStore()
        => new(Guid.NewGuid().ToString(), shared: false, new SharedTestStoreProperties
        {
            ContextType = typeof(Context), OnModelCreating = (_, _) => { },
        });

    [Fact]
    public async Task Boolean_provider_converters_preserve_query_constants_writes_and_stored_values()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: _ => Task.CompletedTask);
        await using (var client = new Context(new DbContextOptionsBuilder<Context>().UseInfoCarrier(store).Options))
        {
            client.AddRange(new FlagRecord { Id = 1, Enabled = new Flag(true) },
                new FlagRecord { Id = 2, Enabled = new Flag(false), Optional = new Flag(true) });
            Assert.Equal(2, await client.SaveChangesAsync(TestContext.Current.CancellationToken));
            var enabled = new Flag(true);
            Assert.Equal(1, await client.Records.Where(row => row.Enabled == enabled).Select(row => row.Id)
                .SingleAsync(TestContext.Current.CancellationToken));
            client.ChangeTracker.Clear();
            FlagRecord first = await client.Records.SingleAsync(row => row.Id == 1, TestContext.Current.CancellationToken);
            Assert.True(first.Enabled.Value);
            Assert.Null(first.Optional);
            FlagRecord second = await client.Records.SingleAsync(row => row.Id == 2, TestContext.Current.CancellationToken);
            Assert.False(second.Enabled.Value);
            Assert.True(second.Optional!.Value);
        }
        await using DbContext server = store.CreateDbContext();
        int[] stored = await server.Database.SqlQueryRaw<int>(
            "SELECT \"Enabled\" AS \"Value\" FROM \"Records\" ORDER BY \"Id\"")
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([1, 0], stored);
    }

    [Fact]
    public async Task Property_bag_complex_collections_restore_nullable_and_primitive_collection_member_types()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: _ => Task.CompletedTask);
        await using (var client = new Context(new DbContextOptionsBuilder<Context>().UseInfoCarrier(store).Options))
        {
            client.Add(new BagRecord
            {
                Id = 1,
                Bags = [new Dictionary<string, object>
                {
                    ["Name"] = "first", ["Numbers"] = new List<int?> { 1, null, 3 },
                    ["Names"] = new List<string> { "one", "two" },
                }],
            });
            Assert.Equal(1, await client.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
        // The upstream property-bag collection materializer remains defective. Read stored JSON
        // independently, so the write assertion does not depend on either entity materializer.
        await using DbContext server = store.CreateDbContext();
        string stored = await server.Database.SqlQueryRaw<string>(
            "SELECT \"Bags\" AS \"Value\" FROM \"BagRecords\"").SingleAsync(TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(stored);
        JsonElement bag = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("first", bag.GetProperty("Name").GetString());
        Assert.Equal([1, null, 3], bag.GetProperty("Numbers").EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.Null ? (int?)null : value.GetInt32()));
        Assert.Equal(["one", "two"], bag.GetProperty("Names").EnumerateArray().Select(value => value.GetString()));
    }

    public sealed record Flag(bool Value);

    public sealed class FlagRecord
    {
        public int Id { get; set; }
        public Flag Enabled { get; set; } = new(false);
        public Flag? Optional { get; set; }
    }

    public sealed class BagRecord
    {
        public int Id { get; set; }
        public List<Dictionary<string, object>> Bags { get; set; } = [];
    }

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<FlagRecord> Records => Set<FlagRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FlagRecord>(entity =>
            {
                entity.Property(row => row.Id).ValueGeneratedNever();
                entity.Property(row => row.Enabled).HasConversion(flag => flag.Value, value => new Flag(value));
                entity.Property(row => row.Optional).HasConversion(flag => flag!.Value, value => new Flag(value));
            });
            modelBuilder.Entity<BagRecord>(entity =>
            {
                entity.ToTable("BagRecords");
                entity.Property(row => row.Id).ValueGeneratedNever();
                entity.ComplexCollection(row => row.Bags, bag =>
                {
                    bag.ToJson();
                    bag.Property<string>("Name");
                    bag.PrimitiveCollection<List<int?>>("Numbers");
                    bag.PrimitiveCollection<List<string>>("Names");
                });
            });
        }
    }
}
