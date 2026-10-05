// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

public class ClientProjectionSetOperationTest
{
    [ConditionalTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Union_after_a_client_method_refuses_before_reading_rows(bool async, bool rightOperand)
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

        foreach (DbContext context in new[] { server, client })
        {
            IQueryable<Row> rows = context.Set<Row>();
            var query = rightOperand
                ? rows.Union(rows.Select(row => ClientRow(row)))
                : rows.Select(row => ClientRow(row)).Union(rows);
            store.ServerSql.Clear();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                if (async) { await query.FirstOrDefaultAsync(); }
                else { query.FirstOrDefault(); }
            });
            Assert.Equal(RelationalStrings.SetOperationsNotAllowedAfterClientEvaluation, exception.Message);
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
