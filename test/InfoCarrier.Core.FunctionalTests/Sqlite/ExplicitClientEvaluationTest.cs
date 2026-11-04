// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

public class ExplicitClientEvaluationTest
{
    [ConditionalTheory]
    [InlineData("filter")]
    [InlineData("order")]
    [InlineData("reverse")]
    [InlineData("distinct")]
    [InlineData("any")]
    public async Task Unregistered_row_decisions_are_refused_without_database_work(string operation)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties { ContextType = typeof(RowContext), OnModelCreating = (_, _) => { } });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.Add(new Row { Id = 1, Name = "one", Unused = "unused" });
            await context.SaveChangesAsync();
        });
        await using RowContext client = new(new DbContextOptionsBuilder().UseInfoCarrier(store).Options);
        IQueryable<Row> rows = client.Set<Row>();
        store.ServerSql.Clear();
        Assert.Throws<InvalidOperationException>(() =>
        {
            switch (operation)
            {
                case "filter": rows.Where(row => new Key(row.Name).Name == "one").ToArray(); break;
                case "order": rows.OrderBy(row => new Key(row.Name).Name).ToArray(); break;
                case "reverse": rows.Select(row => new Key(row.Name)).Reverse().ToArray(); break;
                case "distinct": rows.Select(row => new Key(row.Name)).DistinctBy(key => key).ToArray(); break;
                case "any": rows.Any(row => new Key(row.Name).Name == "one"); break;
            }
        });
        Assert.Empty(store.ServerSql.Statements);

        // The same named key is legal after an explicit local boundary.
        Assert.Equal("one", Assert.Single(rows.AsEnumerable().GroupBy(row => new Key(row.Name))).Key.Name);
        Assert.Single(store.ServerSql.Statements);

        store.ServerSql.Clear();
        Assert.Equal(new Key("one"), Assert.Single(await rows.Select(row => new Key(row.Name)).ToArrayAsync()));
        Assert.DoesNotContain("Unused", Assert.Single(store.ServerSql.Statements), StringComparison.Ordinal);
    }

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Null_component_group_join_uses_backend_semantics_or_explicit_local_execution(bool async)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(RowContext), OnModelCreating = (_, _) => { },
                AllowedTypes = [typeof(Key)],
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(new Row { Id = 1, Name = null, Unused = "unused-null" },
                new Row { Id = 2, Name = "same", Unused = "unused-value" });
            await context.SaveChangesAsync();
        });
        using DbContext server = store.CreateDbContext();
        var capture = new RecordingClient(store);
        await using RowContext registered = new(new DbContextOptionsBuilder().UseInfoCarrier(capture,
            options => options.AddAllowedTypes(typeof(Key))).Options);
        await using RowContext unregistered = new(new DbContextOptionsBuilder().UseInfoCarrier(capture).Options);

        static IQueryable<int> Query(DbContext context)
            => context.Set<Row>().OrderBy(row => row.Id).GroupJoin(context.Set<Row>(),
                outer => new Key(outer.Name), inner => new Key(inner.Name),
                (outer, matches) => matches.Count());

        async Task<int[]> Read(DbContext context)
            => async ? await Query(context).ToArrayAsync() : Query(context).ToArray();

        store.ServerSql.Clear();
        Assert.Equal(new[] { 0, 1 }, await Read(server));
        string direct = Assert.Single(store.ServerSql.Statements);
        Assert.Contains("IS NOT NULL", direct, StringComparison.Ordinal);
        Assert.DoesNotContain("Unused", direct, StringComparison.Ordinal);
        store.ServerSql.Clear();
        Assert.Equal(new[] { 0, 1 }, await Read(registered));
        store.AssertServerSql([direct]);
        QueryDataResult response = Assert.Single(capture.Results);
        Assert.Equal(2, response.Rows.Count);
        Assert.DoesNotContain("unused-", JsonSerializer.Serialize(response.Rows), StringComparison.Ordinal);

        store.ServerSql.Clear();
        capture.Results.Clear();
        int requestsBeforeRefusal = capture.Requests;
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => Read(unregistered));
        Assert.Contains("implicit client execution of 'GroupJoin'", error.Message, StringComparison.Ordinal);
        Assert.Contains("ConfigureShared", error.Message, StringComparison.Ordinal);
        Assert.Contains("AsEnumerable", error.Message, StringComparison.Ordinal);
        Assert.Empty(store.ServerSql.Statements);
        Assert.Empty(capture.Results);
        Assert.Equal(requestsBeforeRefusal, capture.Requests);

        // Explicit local execution retains .NET key equality, including its null match.
        store.ServerSql.Clear();
        Row[] rows = async ? await unregistered.Set<Row>().OrderBy(row => row.Id).ToArrayAsync()
            : unregistered.Set<Row>().OrderBy(row => row.Id).ToArray();
        int[] local = [.. rows.AsEnumerable().GroupJoin(rows,
            outer => new Key(outer.Name), inner => new Key(inner.Name), (_, matches) => matches.Count())];
        Assert.Equal(new[] { 1, 1 }, local);
        Assert.Single(store.ServerSql.Statements);
    }

    private sealed class RecordingClient(IInfoCarrierClient inner) : IInfoCarrierClient
    {
        private readonly IInfoCarrierClient _inner = inner;
        public List<QueryDataResult> Results { get; } = [];
        public int Requests { get; private set; }

        public QueryDataResult QueryData(QueryDataRequest request, DbContext clientContext)
        {
            Requests++;
            QueryDataResult result = _inner.QueryData(request, clientContext);
            Results.Add(result);
            return result;
        }

        public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
        {
            Requests++;
            QueryDataResult result = await _inner.QueryDataAsync(request, clientContext, cancellationToken);
            Results.Add(result);
            return result;
        }

        public SaveChangesResult SaveChanges(SaveChangesRequest request, DbContext clientContext) => _inner.SaveChanges(request, clientContext);
        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, DbContext clientContext, CancellationToken cancellationToken = default) => _inner.SaveChangesAsync(request, clientContext, cancellationToken);
        public TransactionResult BeginTransaction() => _inner.BeginTransaction();
        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default) => _inner.BeginTransactionAsync(cancellationToken);
        public void CommitTransaction(string transactionId) => _inner.CommitTransaction(transactionId);
        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default) => _inner.CommitTransactionAsync(transactionId, cancellationToken);
        public void RollbackTransaction(string transactionId) => _inner.RollbackTransaction(transactionId);
        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default) => _inner.RollbackTransactionAsync(transactionId, cancellationToken);
        public void CreateSavepoint(string transactionId, string name) => _inner.CreateSavepoint(transactionId, name);
        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => _inner.CreateSavepointAsync(transactionId, name, cancellationToken);
        public void RollbackToSavepoint(string transactionId, string name) => _inner.RollbackToSavepoint(transactionId, name);
        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => _inner.RollbackToSavepointAsync(transactionId, name, cancellationToken);
        public void ReleaseSavepoint(string transactionId, string name) => _inner.ReleaseSavepoint(transactionId, name);
        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => _inner.ReleaseSavepointAsync(transactionId, name, cancellationToken);
        public bool SupportsSavepoints(string transactionId) => _inner.SupportsSavepoints(transactionId);
        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default) => _inner.SupportsSavepointsAsync(transactionId, cancellationToken);
    }

    public sealed record Key(string? Name);

    public sealed class Row
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string Unused { get; set; } = "";
    }

    public sealed class RowContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().Property(row => row.Id).ValueGeneratedNever();
    }
}
