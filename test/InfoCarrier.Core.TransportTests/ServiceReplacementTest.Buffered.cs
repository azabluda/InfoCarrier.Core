// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Context = InfoCarrier.Core.TransportTests.ClientServerCoexistenceTest.Context;
using Item = InfoCarrier.Core.TransportTests.ClientServerCoexistenceTest.Item;

namespace InfoCarrier.Core.TransportTests;

public partial class ServiceReplacementTest
{
    [Theory]
    [InlineData("default", false)]
    [InlineData("name", false)]
    [InlineData("handle", false)]
    [InlineData("type", false)]
    [InlineData("default", true)]
    [InlineData("name", true)]
    [InlineData("handle", true)]
    [InlineData("type", true)]
    public async Task Buffered_server_replacement_preserves_streamed_rows_and_failures(string selection, bool fail)
    {
        var observation = new BufferedQueryObservation { Fail = fail };
        await using var host = await Host.Start(selection, custom: false, server =>
        {
            server.Services.AddSingleton(observation);
            server.ReplaceService<IInfoCarrierServer, BufferedServer>();
        });
        using (IServiceScope scope = host.App.Services.CreateScope())
        {
            Context store = scope.ServiceProvider.GetRequiredService<Context>();
            store.AddRange(new Item { Id = 1, Name = "first" }, new Item { Id = 2, Name = "second" });
            await store.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var serializer = new SystemTextJsonInfoCarrierSerializer();
        using HttpClient http = host.App.GetTestClient();
        var client = new TransportInfoCarrierClient(new HttpInfoCarrierTransport(http, serializer), serializer);
        await using var context = new Context(new DbContextOptionsBuilder<Context>().UseInfoCarrier(client).Options);
        IQueryable<Item> query = context.Items.AsNoTracking().OrderBy(item => item.Id);

        if (fail)
        {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => query.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Contains("Buffered replacement refused the query", failure.Message);
        }
        else
        {
            List<Item> rows = await query.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal([1, 2], rows.Select(item => item.Id));
            Assert.Equal(["first", "second"], rows.Select(item => item.Name));
            Assert.Empty(context.ChangeTracker.Entries());
        }

        Assert.Equal(1, observation.Queries);
    }

    public sealed class BufferedQueryObservation
    {
        public bool Fail { get; init; }
        public int Queries;
    }

    // Deliberately implements only the buffered contract. The endpoint must adapt it,
    // rather than assume that every replacement implements IInfoCarrierStreamServer.
    public sealed class BufferedServer(
        [FromKeyedServices] InProcessInfoCarrierServer inner,
        BufferedQueryObservation observation) : IInfoCarrierServer
    {
        public QueryDataResult QueryData(QueryDataRequest request) => inner.QueryData(request);
        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref observation.Queries);
            return observation.Fail
                ? Task.FromException<QueryDataResult>(new InvalidOperationException("Buffered replacement refused the query."))
                : inner.QueryDataAsync(request, cancellationToken);
        }
        public SaveChangesResult SaveChanges(SaveChangesRequest request) => inner.SaveChanges(request);
        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default)
            => inner.SaveChangesAsync(request, cancellationToken);
        public TransactionResult BeginTransaction() => inner.BeginTransaction();
        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
        public void CommitTransaction(string transactionId) => inner.CommitTransaction(transactionId);
        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.CommitTransactionAsync(transactionId, cancellationToken);
        public void RollbackTransaction(string transactionId) => inner.RollbackTransaction(transactionId);
        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.RollbackTransactionAsync(transactionId, cancellationToken);
        public void CreateSavepoint(string transactionId, string name) => inner.CreateSavepoint(transactionId, name);
        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => inner.CreateSavepointAsync(transactionId, name, cancellationToken);
        public void RollbackToSavepoint(string transactionId, string name) => inner.RollbackToSavepoint(transactionId, name);
        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => inner.RollbackToSavepointAsync(transactionId, name, cancellationToken);
        public void ReleaseSavepoint(string transactionId, string name) => inner.ReleaseSavepoint(transactionId, name);
        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => inner.ReleaseSavepointAsync(transactionId, name, cancellationToken);
        public bool SupportsSavepoints(string transactionId) => inner.SupportsSavepoints(transactionId);
        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.SupportsSavepointsAsync(transactionId, cancellationToken);
    }
}
