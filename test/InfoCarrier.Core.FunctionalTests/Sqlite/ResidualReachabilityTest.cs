// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Row = InfoCarrier.Core.FunctionalTests.Sqlite.ExplicitClientEvaluationTest.Row;
using RowContext = InfoCarrier.Core.FunctionalTests.Sqlite.ExplicitClientEvaluationTest.RowContext;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

public class ResidualReachabilityTest
{
    [Fact]
    public async Task A_client_helper_in_an_instance_receiver_keeps_its_translation_diagnostic()
    {
        await using SqliteInfoCarrierBackendTestStore store = await CreateStore();
        await using RowContext client = new(new DbContextOptionsBuilder().UseInfoCarrier(store).Options);
        store.ServerSql.Clear();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => client.Set<Row>()
            .Where(row => LocalName(row.Name).StartsWith("s")).ToArray());

        Assert.Contains(CoreStrings.QueryUnableToTranslateMethod(typeof(ResidualReachabilityTest).FullName!, nameof(LocalName)),
            error.Message, StringComparison.Ordinal);
        Assert.Empty(store.ServerSql.Statements);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overlapping_concat_sources_reuse_deferred_entities_before_tracking(bool async)
    {
        await using SqliteInfoCarrierBackendTestStore store = await CreateStore();
        await using RowContext client = new(new DbContextOptionsBuilder().UseInfoCarrier(store).Options);
        IQueryable<Row> rows = client.Set<Row>();

        // Each source has eligible final client code. Concat keeps all rows without comparing
        // their values. Both responses are materialized before the residual attaches entities.
        var query = rows.Where(row => row.Id <= 2).Select(row => LocalRow(row))
            .Concat(rows.Where(row => row.Id > 0).Select(row => LocalRow(row)));
        store.ServerSql.Clear();
        Row[] results = async ? await query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken) : query.ToArray();

        Assert.Equal(new[] { 1, 2, 1, 2, 3 }, results.Select(row => row.Id));
        Assert.Same(results[0], results[2]);
        Assert.Same(results[1], results[3]);
        Assert.Equal(3, client.ChangeTracker.Entries<Row>().Count());
        Assert.Equal(2, store.ServerSql.Statements.Count);

        store.ServerSql.Clear();
        Row first = async ? await query.FirstAsync(cancellationToken: TestContext.Current.CancellationToken) : query.First();
        Assert.Same(results[0], first);
        Assert.Equal(2, store.ServerSql.Statements.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Paging_between_a_client_projection_and_First_keeps_offset_and_limit_on_the_server(bool async)
    {
        await using SqliteInfoCarrierBackendTestStore store = await CreateStore();
        await using RowContext client = new(new DbContextOptionsBuilder().UseInfoCarrier(store).Options);
        var query = client.Set<Row>().OrderBy(row => row.Id)
            .Select(row => BuildLastRow(row.Id, row.Name)).Skip(1).Take(1);
        store.ServerSql.Clear();

        LastRow result = async ? await query.FirstAsync(cancellationToken: TestContext.Current.CancellationToken) : query.First();

        Assert.Equal(new LastRow(2, "same"), result);
        string statement = Assert.Single(store.ServerSql.Statements);
        Assert.Contains("LIMIT", statement, StringComparison.Ordinal);
        Assert.Contains("OFFSET", statement, StringComparison.Ordinal);
        Assert.DoesNotContain("Unused", statement, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Last_over_a_client_projection_reverses_descending_keys_and_limits_the_server(bool async)
    {
        await using SqliteInfoCarrierBackendTestStore store = await CreateStore();
        using DbContext server = store.CreateDbContext();
        await using RowContext client = new(new DbContextOptionsBuilder().UseInfoCarrier(store).Options);

        async Task<LastRow> Read(DbContext context)
        {
            var query = context.Set<Row>().OrderByDescending(row => row.Name).ThenByDescending(row => row.Id)
                .Select(row => BuildLastRow(row.Id, row.Name));
            return async ? await query.LastAsync() : query.Last();
        }

        LastRow expected = await Read(server);
        Assert.Equal(new LastRow(1, "same"), expected);
        store.ServerSql.Clear();
        Assert.Equal(expected, await Read(client));
        string statement = Assert.Single(store.ServerSql.Statements);
        Assert.Contains("ORDER BY", statement, StringComparison.Ordinal);
        Assert.DoesNotContain(" DESC", statement, StringComparison.Ordinal);
        Assert.Contains("LIMIT 1", statement, StringComparison.Ordinal);
        Assert.DoesNotContain("Unused", statement, StringComparison.Ordinal);
    }

    private static Row LocalRow(Row row) => row;

    private static string LocalName(string? name) => name ?? string.Empty;

    private static LastRow BuildLastRow(int id, string? name) => new(id, name);

    private sealed record LastRow(int Id, string? Name);

    private static async Task<SqliteInfoCarrierBackendTestStore> CreateStore()
    {
        var store = new SqliteInfoCarrierBackendTestStore(Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties { ContextType = typeof(RowContext), OnModelCreating = (_, _) => { } });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(new Row { Id = 1, Name = "same" }, new Row { Id = 2, Name = "same" },
                new Row { Id = 3, Name = "zulu" });
            await context.SaveChangesAsync();
        });
        return store;
    }
}
