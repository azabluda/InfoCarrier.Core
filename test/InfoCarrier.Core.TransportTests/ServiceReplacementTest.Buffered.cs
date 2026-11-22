// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        using var logs = new ClientProcessingLoggingTest.Logs();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using HttpClient http = host.App.GetTestClient();
        var client = new TransportInfoCarrierClient(new HttpInfoCarrierTransport(http, serializer), serializer);
        await using var context = new Context(new DbContextOptionsBuilder<Context>()
            .UseInfoCarrier(client).UseLoggerFactory(loggerFactory).Options);
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
            Assert.Equal("Buffered replacement log", Assert.Single(logs.Entries, entry => entry.Id == 39998).Message);
        }

        Assert.Equal(1, observation.Queries);
    }

    [Fact]
    public async Task Buffered_server_replacement_observes_query_cancellation()
    {
        var observation = new BufferedQueryObservation { WaitForCancellation = true };
        await using var host = await Host.Start("default", custom: false, server =>
        {
            server.Services.AddSingleton(observation);
            server.ReplaceService<IInfoCarrierServer, BufferedServer>();
        });
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        using HttpClient http = host.App.GetTestClient();
        var client = new TransportInfoCarrierClient(new HttpInfoCarrierTransport(http, serializer), serializer);
        await using var context = new Context(new DbContextOptionsBuilder<Context>().UseInfoCarrier(client).Options);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<List<Item>> pending = context.Items.AsNoTracking().ToListAsync(cancellation.Token);

        await observation.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await observation.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, observation.Queries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Default_server_facade_preserves_writes_savepoints_and_stream_capability(bool buffered)
    {
        await using var host = await Host.Start("default", custom: false, server =>
        {
            if (!buffered) { return; }
            server.Services.AddSingleton(new BufferedQueryObservation());
            server.ReplaceService<IInfoCarrierServer, BufferedServer>();
        });
        using IServiceScope scope = host.App.Services.CreateScope();
        IInfoCarrierServer facade = scope.ServiceProvider.GetRequiredService<IInfoCarrierServer>();
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var dispatcher = new InfoCarrierEnvelopeServer(facade, serializer);
        QueryDataRequest? query = null;
        var transport = new ClientProcessingLoggingTest.LocalTransport(dispatcher, response => response, request =>
        {
            if (request.Operation == InfoCarrierOperation.Query) { query = serializer.Deserialize<QueryDataRequest>(request.Payload); }
            return request;
        });
        var client = new TransportInfoCarrierClient(transport, serializer);
        using var context = new Context(new DbContextOptionsBuilder<Context>().UseInfoCarrier(client).Options);
        using (var transaction = context.Database.BeginTransaction())
        {
            Assert.True(transaction.SupportsSavepoints);
            context.Add(new Item { Id = 1, Name = "retained" });
            Assert.Equal(1, context.SaveChanges());
            transaction.CreateSavepoint("retained");
            context.Add(new Item { Id = 2, Name = "discarded" });
            Assert.Equal(1, context.SaveChanges());
            transaction.RollbackToSavepoint("retained");
            transaction.ReleaseSavepoint("retained");
            transaction.Commit();
        }
        Assert.Equal("retained", Assert.Single(context.Items.AsNoTracking().ToList()).Name);
        var frames = new List<QueryStreamFrame>();
        await foreach (QueryStreamFrame frame in ((IInfoCarrierStreamServer)facade)
            .QueryFramesAsync(query!, TestContext.Current.CancellationToken)) { frames.Add(frame); }
        Assert.IsType<QueryStartFrame>(frames[0]);
        Assert.Single(frames.OfType<QueryRowFrame>());
        Assert.IsType<QueryCompleteFrame>(frames[^1]);
        if (buffered) { Assert.Equal("Buffered replacement log", Assert.Single(frames.OfType<QueryLogFrame>()).Event.Message); }
        Assert.Equal("retained", (await scope.ServiceProvider.GetRequiredService<Context>().Items.SingleAsync(
            TestContext.Current.CancellationToken)).Name);
    }

    public sealed class BufferedQueryObservation
    {
        public bool Fail { get; init; }
        public bool WaitForCancellation { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Queries;
    }

    // Deliberately implements only the buffered contract. The endpoint must adapt it,
    // rather than assume that every replacement implements IInfoCarrierStreamServer.
    public sealed class BufferedServer(
        [FromKeyedServices] InProcessInfoCarrierServer inner,
        BufferedQueryObservation observation) : IInfoCarrierServer
    {
        public QueryDataResult QueryData(QueryDataRequest request) => WithLog(inner.QueryData(request));
        public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref observation.Queries);
            observation.Started.TrySetResult();
            if (observation.WaitForCancellation)
            {
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { observation.Canceled.TrySetResult(); throw; }
            }
            if (observation.Fail) { throw new InvalidOperationException("Buffered replacement refused the query."); }
            return WithLog(await inner.QueryDataAsync(request, cancellationToken));
        }
        private static QueryDataResult WithLog(QueryDataResult result) => result with
        {
            ServerLog = [new ServerLogEvent
            {
                Category = "Test.BufferedReplacement", EventId = 39998, Level = (int)LogLevel.Warning,
                Message = "Buffered replacement log",
            }],
        };
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
