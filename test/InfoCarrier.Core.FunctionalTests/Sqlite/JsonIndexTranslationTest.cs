// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using InfoCarrier.Core.Query;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>Registered JSON index helpers reach the backend; private helpers run after materialization.</summary>
public class JsonIndexTranslationTest
{
    [ConditionalTheory]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, false, false)]
    public async Task Json_index_distinguishes_registered_parity_from_private_local_indexing(bool async, bool nested, bool clientIndex, bool registered)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(JsonContext),
                AllowedTypes = registered ? [typeof(JsonIndexTranslationTest)] : [],
                OnModelCreating = (_, _) => { },
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(
                new Owner { Id = 1, Roots = [Root("alpha", "beta"), Root("beta", "alpha")] },
                new Owner { Id = 2, Roots = [Root("gamma", "delta"), Root("delta", "gamma")] });
            await context.SaveChangesAsync();
        });
        using DbContext server = store.CreateDbContext();
        await using JsonContext client = new(new DbContextOptionsBuilder<JsonContext>().UseInfoCarrier(store,
            options => options.AllowTypes(registered ? [typeof(JsonIndexTranslationTest)] : [])).Options);

        async Task<string[]> Execute(DbContext context)
        {
            if (nested)
            {
                var query = clientIndex
                    ? registered
                        ? context.Set<Owner>().Select(owner => owner.Roots[0].Leaves[SharedIndex(owner.Id)]).AsNoTracking()
                        : context.Set<Owner>().Select(owner => owner.Roots[0].Leaves[MyIndex(owner.Id)]).AsNoTracking()
                    : context.Set<Owner>().Select(owner => owner.Roots[0].Leaves[owner.Id - 1]).AsNoTracking();
                var rows = async ? await query.ToArrayAsync() : query.ToArray();
                return [.. rows.Select(row => row.Name).OrderBy(name => name, StringComparer.Ordinal)];
            }
            else
            {
                var query = clientIndex
                    ? registered
                        ? context.Set<Owner>().Select(owner => owner.Roots[SharedIndex(owner.Id)]).AsNoTracking()
                        : context.Set<Owner>().Select(owner => owner.Roots[MyIndex(owner.Id)]).AsNoTracking()
                    : context.Set<Owner>().Select(owner => owner.Roots[owner.Id - 1]).AsNoTracking();
                var rows = async ? await query.ToArrayAsync() : query.ToArray();
                return [.. rows.Select(row => row.Name).OrderBy(name => name, StringComparer.Ordinal)];
            }
        }

        if (clientIndex && registered)
        {
            // Only shared public methods demand parity. Private helpers pin ICC behavior alone.
            foreach (DbContext context in registered ? new DbContext[] { server, client } : [client])
            {
                store.ServerSql.Clear();
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(context));
                Assert.Contains(registered ? nameof(SharedIndex) : nameof(MyIndex), exception.Message, StringComparison.Ordinal);
                if (registered)
                {
                    Assert.DoesNotContain("QuerySplitter", exception.ToString(), StringComparison.Ordinal);
                    Assert.Contains("could not be translated", exception.Message, StringComparison.Ordinal);
                    if (ReferenceEquals(context, client))
                    {
                        string serverStack = Assert.IsType<string>(exception.Data[InfoCarrierFaultMapper.ServerStackTraceKey]);
                        Assert.Contains("Microsoft.EntityFrameworkCore.Query", serverStack, StringComparison.Ordinal);
                    }
                }
                Assert.Empty(store.ServerSql.Statements);
            }
        }
        else if (clientIndex)
        {
            store.ServerSql.Clear();
            Assert.Equal(new[] { "alpha", "delta" }, await Execute(client));
            string statement = Assert.Single(store.ServerSql.Statements);
            Assert.DoesNotContain(nameof(MyIndex), statement, StringComparison.Ordinal);
        }
        else
        {
            store.ServerSql.Clear();
            Assert.Equal(new[] { "alpha", "delta" }, await Execute(server));
            string[] direct = [.. store.ServerSql.Statements];
            Assert.Single(direct);
            store.ServerSql.Clear();
            Assert.Equal(new[] { "alpha", "delta" }, await Execute(client));
            store.AssertServerSql(direct);
        }
    }

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_mapped_instance_function_can_supply_a_JSON_index(bool async)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(JsonContext),
                OnModelCreating = (_, _) => { },
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.Add(new Owner { Id = 1, Roots = [Root("alpha", "beta")] });
            await context.SaveChangesAsync();
        });
        using JsonContext server = (JsonContext)store.CreateDbContext();
        await using JsonContext client = new(new DbContextOptionsBuilder<JsonContext>().UseInfoCarrier(store).Options);

        async Task<string[]> Execute(JsonContext context)
        {
            var query = context.Set<Owner>().Select(owner => owner.Roots[context.SqlIndex(owner.Id - 1)]).AsNoTracking();
            var rows = async ? await query.ToArrayAsync() : query.ToArray();
            return [.. rows.Select(row => row.Name)];
        }

        store.ServerSql.Clear();
        Assert.Equal(new[] { "alpha" }, await Execute(server));
        string[] direct = [.. store.ServerSql.Statements];
        Assert.Single(direct);
        store.ServerSql.Clear();
        Assert.Equal(new[] { "alpha" }, await Execute(client));
        store.AssertServerSql(direct);
    }

    [ConditionalFact]
    public void A_table_owned_occurrence_is_not_classified_by_another_occurrences_JSON_mapping()
    {
        using MixedContext context = new(new DbContextOptionsBuilder<MixedContext>()
            .UseSqlite("Data Source=:memory:").Options);
        var query = context.Set<MixedOwner>().Select(owner => owner.TableRoot.Leaves[MyIndex(owner.Id)]);

        // This checks the split boundary, not whether SQLite translates the remaining query.
        // The unrelated JSON ownership must not trigger the early JSON-specific refusal.
        new QuerySplitter(context.Model).Split(query.Expression);
    }

    public static int SharedIndex(int id) => id - 1;

    private static int MyIndex(int id) => id - 1;

    private static RootItem Root(string first, string second)
        => new() { Name = first, Leaves = [new() { Name = first }, new() { Name = second }] };

    public sealed class Owner
    {
        public int Id { get; set; }
        public List<RootItem> Roots { get; set; } = [];
    }

    public sealed class RootItem
    {
        public string Name { get; set; } = "";
        public List<LeafItem> Leaves { get; set; } = [];
    }

    public sealed class LeafItem
    {
        public string Name { get; set; } = "";
    }

    public sealed class JsonContext(DbContextOptions options) : DbContext(options)
    {
        public int SqlIndex(int value) => throw new NotSupportedException("Only a mapped SQL call may execute this method.");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDbFunction(typeof(JsonContext).GetMethod(nameof(SqlIndex))!).HasName("abs").IsBuiltIn();
            modelBuilder.Entity<Owner>().Property(owner => owner.Id).ValueGeneratedNever();
            modelBuilder.Entity<Owner>().OwnsMany(owner => owner.Roots, roots =>
            {
                roots.ToJson();
                roots.OwnsMany(root => root.Leaves);
            });
        }
    }

    public sealed class MixedOwner
    {
        public int Id { get; set; }
        public RootItem JsonRoot { get; set; } = new();
        public RootItem TableRoot { get; set; } = new();
    }

    public sealed class MixedContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MixedOwner>().OwnsOne(owner => owner.JsonRoot, root =>
            {
                root.ToJson();
                root.OwnsMany(item => item.Leaves);
            });
            modelBuilder.Entity<MixedOwner>().OwnsOne(owner => owner.TableRoot,
                root => root.OwnsMany(item => item.Leaves));
        }
    }
}
