// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>Final scalar projections do not wrap an inheritance union in a subquery.</summary>
public class InheritanceProjectionSqlTest
{
    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_one_column_projection_over_a_derived_hierarchy_keeps_EFs_union(bool async)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(InheritanceContext),
                OnModelCreating = (_, _) => { },
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(
                new Eagle { Id = 1, Name = "alpha" },
                new Eagle { Id = 2, Name = null },
                new Kiwi { Id = 3, Name = "beta" });
            await context.SaveChangesAsync();
        });

        using DbContext server = store.CreateDbContext();
        await using InheritanceContext client = new(new DbContextOptionsBuilder<InheritanceContext>()
            .UseInfoCarrier(store).Options);

        async Task<string?[]> Execute(DbContext context)
        {
            var query = context.Set<Bird>().Select(bird => new { bird.Name });
            var rows = async ? await query.ToArrayAsync() : query.ToArray();
            return [.. rows.Select(row => row.Name).OrderBy(name => name, StringComparer.Ordinal)];
        }

        store.ServerSql.Clear();
        Assert.Equal(new string?[] { null, "alpha", "beta" }, await Execute(server));
        string[] direct = [.. store.ServerSql.Statements];
        Assert.Contains("UNION ALL", Assert.Single(direct), StringComparison.Ordinal);
        Assert.DoesNotContain("FROM (", Assert.Single(direct), StringComparison.Ordinal);

        store.ServerSql.Clear();
        Assert.Equal(new string?[] { null, "alpha", "beta" }, await Execute(client));
        store.AssertServerSql(direct);
    }

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_two_column_shadow_projection_keeps_EFs_union(bool async)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(InheritanceContext),
                OnModelCreating = (_, _) => { },
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            Animal[] animals =
            [
                new Eagle { Id = 1, Name = "alpha" },
                new Eagle { Id = 2, Name = null },
                new Kiwi { Id = 3, Name = "beta" },
            ];
            context.AddRange(animals);
            context.Entry(animals[0]).Property("Habitat").CurrentValue = "mountain";
            context.Entry(animals[2]).Property("Habitat").CurrentValue = "forest";
            await context.SaveChangesAsync();
        });

        using DbContext server = store.CreateDbContext();
        await using InheritanceContext client = new(new DbContextOptionsBuilder<InheritanceContext>()
            .UseInfoCarrier(store).Options);

        async Task<(string? Name, string? Habitat)[]> Execute(DbContext context)
        {
            var query = context.Set<Bird>().Select(bird => new
            {
                bird.Name,
                Habitat = EF.Property<string?>(bird, "Habitat"),
            });
            var rows = async ? await query.ToArrayAsync() : query.ToArray();
            return [.. rows.OrderBy(row => row.Name, StringComparer.Ordinal)
                .Select(row => (row.Name, row.Habitat))];
        }

        (string? Name, string? Habitat)[] expected = [(null, null), ("alpha", "mountain"), ("beta", "forest")];
        store.ServerSql.Clear();
        Assert.Equal(expected, await Execute(server));
        string[] direct = [.. store.ServerSql.Statements];
        Assert.Contains("UNION ALL", Assert.Single(direct), StringComparison.Ordinal);
        Assert.DoesNotContain("FROM (", Assert.Single(direct), StringComparison.Ordinal);

        store.ServerSql.Clear();
        Assert.Equal(expected, await Execute(client));
        store.AssertServerSql(direct);
    }

    public abstract class Animal
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public abstract class Bird : Animal;
    public sealed class Eagle : Bird;
    public sealed class Kiwi : Bird;

    public sealed class InheritanceContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Animal>().UseTpcMappingStrategy();
            modelBuilder.Entity<Animal>().Property(animal => animal.Id).ValueGeneratedNever();
            modelBuilder.Entity<Animal>().Property<string?>("Habitat");
            modelBuilder.Entity<Bird>();
            modelBuilder.Entity<Eagle>();
            modelBuilder.Entity<Kiwi>();
        }
    }
}
