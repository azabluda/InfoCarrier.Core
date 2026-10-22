// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Runtime.CompilerServices;
using InfoCarrier.Core.Query;
using Microsoft.EntityFrameworkCore;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class StreamingQueryRegressionTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task Non_tracking_query_yields_before_its_frame_stream_completes()
    {
        var gate = new GatedClient(CreateInnerClient());
        using NorthwindContext context = CreateContext(gate);

        await AssertFirstResultPrecedesCompletion(
            context.Customers.AsNoTracking().OrderBy(customer => customer.Id).AsAsyncEnumerable(),
            gate,
            customer => Assert.Equal("ALFKI", customer.Id));
    }

    [Fact]
    public async Task Row_local_residual_yields_before_its_frame_stream_completes()
    {
        var gate = new GatedClient(CreateInnerClient());
        using NorthwindContext context = CreateContext(gate);
        IAsyncEnumerable<CustomerCard> query = context.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Id)
            .Select(customer => new CustomerCard(customer.Id))
            .AsAsyncEnumerable();

        await AssertFirstResultPrecedesCompletion(
            query,
            gate,
            customer => Assert.Equal("ALFKI", customer.Id));
    }

    [Fact]
    public async Task Concatenated_query_yields_before_its_frame_stream_completes()
    {
        var gate = new GatedClient(CreateInnerClient());
        using NorthwindContext context = CreateContext(gate);
        var first = context.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == "ALFKI")
            .Select(customer => new { customer.Id });
        var second = context.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == "BLAUS")
            .Select(customer => new { customer.Id });

        await AssertFirstResultPrecedesCompletion(
            first.Concat(second).AsAsyncEnumerable(),
            gate,
            customer => Assert.Equal("ALFKI", customer.Id));
    }

    [Fact]
    public async Task Tracked_query_waits_for_buffered_materialization()
    {
        var gate = new GatedClient(CreateInnerClient());
        using NorthwindContext context = CreateContext(gate);
        await using IAsyncEnumerator<Customer> rows = context.Customers
            .OrderBy(customer => customer.Id)
            .AsAsyncEnumerable()
            .GetAsyncEnumerator();

        Task<bool> firstMove = rows.MoveNextAsync().AsTask();
        await gate.BufferedCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(firstMove.IsCompleted);

        gate.Release.TrySetResult();
        Assert.True(await firstMove.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("ALFKI", rows.Current.Id);
    }

    private async Task AssertFirstResultPrecedesCompletion<T>(
        IAsyncEnumerable<T> query,
        GatedClient gate,
        Action<T> assertFirst)
    {
        await using IAsyncEnumerator<T> rows = query.GetAsyncEnumerator();
        Task<bool> firstMove = rows.MoveNextAsync().AsTask();
        await gate.FirstRowOffered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            Assert.True(await firstMove.WaitAsync(TimeSpan.FromSeconds(5)));
            assertFirst(rows.Current);
            Assert.False(gate.StreamCompleted);
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        while (await rows.MoveNextAsync())
        {
        }
    }

    private IInfoCarrierClient CreateInnerClient()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        return new TransportInfoCarrierClient(
            new HttpInfoCarrierTransport(factory.CreateClient(), serializer), serializer);
    }

    private static NorthwindContext CreateContext(IInfoCarrierClient client)
        => new(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseInfoCarrier(client)
                .Options);

    private sealed record CustomerCard(string Id);

    private sealed class GatedClient(IInfoCarrierClient inner) : IInfoCarrierClient
    {
        public TaskCompletionSource FirstRowOffered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource BufferedCallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool StreamCompleted { get; private set; }

        public async IAsyncEnumerable<QueryStreamFrame> QueryDataStreamAsync(
            Common.QueryDataRequest request,
            DbContext clientContext,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            bool rowSeen = false;
            await foreach (QueryStreamFrame frame in inner
                .QueryDataStreamAsync(request, clientContext, cancellationToken)
                .WithCancellation(cancellationToken))
            {
                if (rowSeen)
                {
                    await Release.Task.WaitAsync(cancellationToken);
                }

                if (frame is QueryRowFrame)
                {
                    rowSeen = true;
                    FirstRowOffered.TrySetResult();
                }

                yield return frame;
            }

            StreamCompleted = true;
        }

        public async Task<Common.QueryDataResult> QueryDataAsync(
            Common.QueryDataRequest request,
            DbContext clientContext,
            CancellationToken cancellationToken = default)
        {
            BufferedCallStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return await inner.QueryDataAsync(request, clientContext, cancellationToken);
        }

        public Task<Common.SaveChangesResult> SaveChangesAsync(
            Common.SaveChangesRequest request,
            DbContext clientContext,
            CancellationToken cancellationToken = default)
            => inner.SaveChangesAsync(request, clientContext, cancellationToken);

        public Task<Common.TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);

        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.CommitTransactionAsync(transactionId, cancellationToken);

        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.RollbackTransactionAsync(transactionId, cancellationToken);

        public Task CreateSavepointAsync(
            string transactionId,
            string name,
            CancellationToken cancellationToken = default)
            => inner.CreateSavepointAsync(transactionId, name, cancellationToken);

        public Task RollbackToSavepointAsync(
            string transactionId,
            string name,
            CancellationToken cancellationToken = default)
            => inner.RollbackToSavepointAsync(transactionId, name, cancellationToken);

        public Task ReleaseSavepointAsync(
            string transactionId,
            string name,
            CancellationToken cancellationToken = default)
            => inner.ReleaseSavepointAsync(transactionId, name, cancellationToken);

        public Task<bool> SupportsSavepointsAsync(
            string transactionId,
            CancellationToken cancellationToken = default)
            => inner.SupportsSavepointsAsync(transactionId, cancellationToken);
    }
}
