// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

public class SharedOwnedRowUpdateTest
{
    [ConditionalTheory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, true)]
    public async Task Changes_in_two_owned_branches_match_the_direct_write(
        bool async, bool replace, bool separateTables, bool leafOnly)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties
            {
                ContextType = separateTables ? typeof(SeparateContext) : typeof(RowContext),
                OnModelCreating = (_, _) => { },
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(Create(1), Create(2));
            await context.SaveChangesAsync();
        });
        using DbContext server = store.CreateDbContext();
        var options = new DbContextOptionsBuilder().UseInfoCarrier(store).Options;
        await using RowContext client = separateTables ? new SeparateContext(options) : new RowContext(options);

        async Task<int> Change(DbContext context, int id)
        {
            var query = context.Set<Owner>().Where(owner => owner.Id == id);
            Owner owner = async ? await query.SingleAsync() : query.Single();
            if (replace)
            {
                owner.Left = new Branch { Name = "left-new", Nested = new Leaf { Name = "left-leaf-new" } };
                owner.Right = new Branch { Name = "right-new", Nested = new Leaf { Name = "right-leaf-new" } };
            }
            else
            {
                if (!leafOnly) { owner.Left.Name = "left-new"; }
                owner.Left.Nested.Name = "left-leaf-new";
                if (!leafOnly) { owner.Right.Name = "right-new"; }
                owner.Right.Nested.Name = "right-leaf-new";
            }

            store.ServerSql.Clear();
            int count = async ? await context.SaveChangesAsync() : context.SaveChanges();
            Assert.False(context.ChangeTracker.HasChanges());
            return count;
        }

        int directCount = await Change(server, 1);
        string[] direct = [.. store.ServerSql.Statements];
        Assert.Equal(separateTables ? 2 : 1, direct.Length);
        Assert.Equal(directCount, await Change(client, 2));
        store.AssertServerSql(direct);

        using DbContext verify = store.CreateDbContext();
        Owner[] saved = await verify.Set<Owner>().OrderBy(owner => owner.Id).ToArrayAsync();
        Assert.Equal(2, saved.Length);
        foreach (Owner owner in saved)
        {
            Assert.Equal("untouched", owner.Label);
            Assert.Equal(leafOnly ? "left" : "left-new", owner.Left.Name);
            Assert.Equal("left-leaf-new", owner.Left.Nested.Name);
            Assert.Equal(leafOnly ? "right" : "right-new", owner.Right.Name);
            Assert.Equal("right-leaf-new", owner.Right.Nested.Name);
        }
    }

    private static Owner Create(int id)
        => new()
        {
            Id = id,
            Label = "untouched",
            Left = new Branch { Name = "left", Nested = new Leaf { Name = "left-leaf" } },
            Right = new Branch { Name = "right", Nested = new Leaf { Name = "right-leaf" } },
        };

    public sealed class Owner
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
        public Branch Left { get; set; } = new();
        public Branch Right { get; set; } = new();
    }

    public sealed class Branch
    {
        public string Name { get; set; } = "";
        public Leaf Nested { get; set; } = new();
    }

    public sealed class Leaf
    {
        public string Name { get; set; } = "";
    }

    public class RowContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Owner>().Property(owner => owner.Id).ValueGeneratedNever();
            modelBuilder.Entity<Owner>().OwnsOne(owner => owner.Left, branch => branch.OwnsOne(value => value.Nested));
            modelBuilder.Entity<Owner>().OwnsOne(owner => owner.Right, branch => branch.OwnsOne(value => value.Nested));
        }
    }

    public sealed class SeparateContext(DbContextOptions options) : RowContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Owner>().OwnsOne(owner => owner.Left).ToTable("Left");
            modelBuilder.Entity<Owner>().OwnsOne(owner => owner.Right).ToTable("Right");
        }
    }
}
