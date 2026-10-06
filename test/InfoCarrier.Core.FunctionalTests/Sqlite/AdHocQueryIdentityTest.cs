// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Text.Json;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

public class AdHocQueryIdentityTest
{
    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_constant_raw_query_preserves_its_identity_in_the_wire_node(bool mapped)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(RowContext),
                OnModelCreating = (_, _) => { },
                ArbitrarySqlExecution = true,
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext);
        var options = new DbContextOptionsBuilder().UseInfoCarrier(store, builder => builder.AllowArbitrarySqlExecution()).Options;
        await using RowContext client = new(options);
        IQueryable<Row> query = mapped
            ? client.Set<Row>().FromSqlRaw("SELECT * FROM Rows")
            : client.Database.SqlQueryRaw<Row>("SELECT * FROM Rows");
        var serializer = (ExpressionSerializer)client.GetService<IExpressionSerializer>();
        ExpressionNode node = serializer.ToNode(Expression.Constant(query, typeof(IQueryable<Row>)));
        string json = JsonSerializer.Serialize(node, ExpressionJsonContext.Default.ExpressionNode);
        var restored = Assert.IsType<FromSqlQueryRootStubNode>(
            JsonSerializer.Deserialize(json, ExpressionJsonContext.Default.ExpressionNode));

        Assert.Equal(!mapped, restored.IsAdHoc);
        using JsonDocument payload = JsonDocument.Parse(json);
        Assert.Equal(!mapped, payload.RootElement.TryGetProperty("isAdHoc", out JsonElement marker));
        if (!mapped) { Assert.True(marker.GetBoolean()); }
    }

    [ConditionalTheory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task Raw_query_identity_preserves_the_direct_filter_behavior(bool async, bool mapped, bool composed)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(RowContext),
                OnModelCreating = (_, _) => { },
                ArbitrarySqlExecution = true,
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
            await context.Database.ExecuteSqlRawAsync("INSERT INTO Rows (Value) VALUES ('visible'), ('excluded')"));
        using DbContext server = store.CreateDbContext();
        var options = new DbContextOptionsBuilder().UseInfoCarrier(store, builder => builder.AllowArbitrarySqlExecution()).Options;
        await using RowContext client = new(options);

        async Task<string[]> Read(DbContext context)
        {
            IQueryable<Row> query = mapped
                ? context.Set<Row>().FromSqlRaw("SELECT * FROM Rows")
                : context.Database.SqlQueryRaw<Row>("SELECT * FROM Rows");
            if (composed) { query = query.Where(row => row.Value == "excluded"); }
            Row[] rows = async ? await query.ToArrayAsync() : query.ToArray();
            return rows.Select(row => row.Value).Order().ToArray();
        }

        string[] expected = mapped
            ? composed ? [] : ["visible"]
            : composed ? ["excluded"] : ["excluded", "visible"];
        store.ServerSql.Clear();
        Assert.Equal(expected, await Read(server));
        string[] direct = [.. store.ServerSql.Statements];
        Assert.Single(direct);
        store.ServerSql.Clear();
        Assert.Equal(expected, await Read(client));
        store.AssertServerSql(direct);
    }

    public sealed class Row
    {
        public string Value { get; set; } = "";
    }

    public sealed class RowContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().HasNoKey().ToTable("Rows").HasQueryFilter(row => row.Value != "excluded");
    }
}
