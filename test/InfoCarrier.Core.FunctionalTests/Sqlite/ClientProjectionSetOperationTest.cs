// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

public class ClientProjectionSetOperationTest
{
    [ConditionalTheory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task Union_distinguishes_registered_parity_from_private_local_execution(bool async, bool rightOperand, bool registered)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SetContext), OnModelCreating = (_, _) => { },
                AllowedTypes = registered ? [typeof(ClientProjectionSetOperationTest)] : [],
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(new Row { Id = 1, Name = "alpha" }, new Row { Id = 2, Name = "beta" });
            await context.SaveChangesAsync();
        });
        using DbContext server = store.CreateDbContext();
        await using SetContext client = new(new DbContextOptionsBuilder<SetContext>().UseInfoCarrier(store,
            options => options.AllowTypes(registered ? [typeof(ClientProjectionSetOperationTest)] : [])).Options);

        // Unregistered private helpers characterize ICC alone, not the backend's contract.
        foreach (DbContext context in registered ? new DbContext[] { server, client } : [client])
        {
            IQueryable<Row> rows = context.Set<Row>();
            var projection = registered
                ? rows.Select(row => SharedRow(row))
                : rows.Select(row => ClientRow(row));
            var query = rightOperand ? rows.Union(projection) : projection.Union(rows);
            store.ServerSql.Clear();
            if (!registered)
            {
                Row? result = async ? await query.FirstOrDefaultAsync() : query.FirstOrDefault();
                Assert.NotNull(result);
                Assert.Equal("alpha", result.Name);
                Assert.Equal(2, store.ServerSql.Statements.Count);
                Assert.All(store.ServerSql.Statements, statement =>
                    Assert.DoesNotContain("UNION", statement, StringComparison.Ordinal));
                continue;
            }

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                if (async) { await query.FirstOrDefaultAsync(); }
                else { query.FirstOrDefault(); }
            });
            Assert.Equal(RelationalStrings.SetOperationsNotAllowedAfterClientEvaluation, exception.Message);
            if (registered)
            {
                Assert.DoesNotContain("QuerySplitter", exception.ToString(), StringComparison.Ordinal);
                if (ReferenceEquals(context, client))
                {
                    string serverStack = Assert.IsType<string>(exception.Data[InfoCarrierFaultMapper.ServerStackTraceKey]);
                    Assert.Contains("Microsoft.EntityFrameworkCore.Query", serverStack, StringComparison.Ordinal);
                }
            }
            Assert.Empty(store.ServerSql.Statements);
        }
    }

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Union_of_constructed_scalar_projections_stays_on_the_server(bool async)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties { ContextType = typeof(SetContext), OnModelCreating = (_, _) => { } });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(new Row { Id = 1, Name = "alpha" }, new Row { Id = 2, Name = "beta" });
            await context.SaveChangesAsync();
        });
        using DbContext server = store.CreateDbContext();
        await using SetContext client = new(new DbContextOptionsBuilder<SetContext>().UseInfoCarrier(store).Options);

        async Task<string[]> Execute(DbContext context)
        {
            var rows = context.Set<Row>().Select(row => new { row.Id, row.Name });
            var query = rows.Union(rows).OrderBy(row => row.Id);
            var result = async ? await query.ToArrayAsync() : query.ToArray();
            return [.. result.Select(row => row.Name)];
        }

        store.ServerSql.Clear();
        Assert.Equal(new[] { "alpha", "beta" }, await Execute(server));
        string[] direct = [.. store.ServerSql.Statements];
        Assert.Single(direct);
        store.ServerSql.Clear();
        Assert.Equal(new[] { "alpha", "beta" }, await Execute(client));
        string statement = Assert.Single(store.ServerSql.Statements);
        Assert.Contains("UNION", statement, StringComparison.Ordinal);
    }

    [ConditionalTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Registered_public_Union_preserves_nonrelational_backend_failures(bool async, bool rightOperand)
    {
        await using InMemoryInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SetContext), OnModelCreating = (_, _) => { },
                AllowedTypes = [typeof(ClientProjectionSetOperationTest)],
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(new Row { Id = 1, Name = "alpha" }, new Row { Id = 2, Name = "beta" });
            await context.SaveChangesAsync();
        });
        using DbContext server = store.CreateDbContext();
        await using SetContext client = new(new DbContextOptionsBuilder<SetContext>().UseInfoCarrier(store,
            options => options.UseNonRelationalServerStore().AllowTypes(typeof(ClientProjectionSetOperationTest))).Options);

        async Task Execute(DbContext context)
        {
            IQueryable<Row> rows = context.Set<Row>();
            IQueryable<Row> projection = rows.Select(row => SharedRow(row));
            var query = rightOperand ? rows.Union(projection) : projection.Union(rows);
            if (async) { await query.FirstOrDefaultAsync(); }
            else { query.FirstOrDefault(); }
        }

        Exception direct = Assert.IsAssignableFrom<Exception>(await Record.ExceptionAsync(() => Execute(server)));
        Exception carried = Assert.IsAssignableFrom<Exception>(await Record.ExceptionAsync(() => Execute(client)));
        Assert.Equal(direct.GetType(), carried.GetType());
        Assert.Equal(direct.Message, carried.Message);
        string serverStack = Assert.IsType<string>(carried.Data[InfoCarrierFaultMapper.ServerStackTraceKey]);
        Assert.Contains("Microsoft.EntityFrameworkCore.InMemory.Query", serverStack, StringComparison.Ordinal);
        if (rightOperand) { Assert.IsType<KeyNotFoundException>(direct); }
        else
        {
            Assert.IsType<InvalidOperationException>(direct);
            Assert.Contains("set operation after client projection", direct.Message, StringComparison.Ordinal);
        }
    }

    public static Row SharedRow(Row row) => row;

    private static Row ClientRow(Row row) => row;

    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class SetContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().Property(row => row.Id).ValueGeneratedNever();
    }
}
