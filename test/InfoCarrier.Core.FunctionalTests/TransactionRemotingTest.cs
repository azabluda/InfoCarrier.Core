// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     Transactions are remoted (roadmap M4): begin, commit and rollback are round trips, and
///     every request in between carries the server's token (wire-protocol W3).
/// </summary>
/// <remarks>
///     <para>
///         This replaces <c>TransactionIgnoredTest</c>, which asserted the arrangement M4
///         supersedes — the client returning a stub and raising
///         <c>InfoCarrierEventId.TransactionIgnoredWarning</c> on the store's behalf. The client
///         no longer decides: it asks the store, and a store that does not do transactions raises
///         its own warning on its own side.
///     </para>
///     <para>
///         Direct assertions rather than a suite count, for the reason the file they replace gave:
///         on the InMemory tier the remoted transaction still ends up doing nothing, so no number
///         in a suite run distinguishes "the token flows" from "the token is never sent".
///     </para>
/// </remarks>
public class TransactionRemotingTest
{
    [Fact]
    public void Beginning_a_transaction_round_trips_and_becomes_the_current_transaction()
    {
        var client = new RecordingClient();
        using var context = new SmokeContext(Options(client));

        using IDbContextTransaction transaction = context.Database.BeginTransaction();

        Assert.Equal(1, client.Begins);
        Assert.Equal("server-token-1", Assert.IsType<InfoCarrierTransaction>(transaction).ServerTransactionId);
        Assert.Same(transaction, context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task A_save_inside_a_transaction_carries_its_token()
    {
        var client = new RecordingClient();
        await using var context = new SmokeContext(Options(client));

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        context.Blogs.Add(new Blog { Title = "t" });
        await context.SaveChangesAsync();

        Assert.Equal("server-token-1", Assert.Single(client.SaveTokens));
    }

    [Fact]
    public async Task A_save_outside_a_transaction_carries_no_token()
    {
        var client = new RecordingClient();
        await using var context = new SmokeContext(Options(client));

        context.Blogs.Add(new Blog { Title = "t" });
        await context.SaveChangesAsync();

        Assert.Null(Assert.Single(client.SaveTokens));
    }

    [Fact]
    public async Task Committing_round_trips_and_clears_the_current_transaction()
    {
        var client = new RecordingClient();
        await using var context = new SmokeContext(Options(client));

        IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        await transaction.CommitAsync();

        Assert.Equal(["commit:server-token-1"], client.Ends);
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Disposing_an_uncommitted_transaction_rolls_it_back_on_the_server()
    {
        var client = new RecordingClient();
        await using var context = new SmokeContext(Options(client));

        await using (await context.Database.BeginTransactionAsync())
        {
        }

        // Otherwise the server keeps a scope, a context and an open store transaction for a
        // client that has walked away (requirements §2.9).
        Assert.Equal(["rollback:server-token-1"], client.Ends);
    }

    [Fact]
    public async Task Committing_then_disposing_does_not_roll_back_afterwards()
    {
        var client = new RecordingClient();
        await using var context = new SmokeContext(Options(client));

        await using (IDbContextTransaction transaction = await context.Database.BeginTransactionAsync())
        {
            await transaction.CommitAsync();
        }

        Assert.Equal(["commit:server-token-1"], client.Ends);
    }

    [Fact]
    public async Task A_second_transaction_on_the_same_context_is_refused()
    {
        var client = new RecordingClient();
        await using var context = new SmokeContext(Options(client));

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Database.BeginTransactionAsync());

        Assert.Contains("already open", exception.Message);
    }

    [Fact]
    public async Task Savepoints_are_addressed_by_the_transaction_token_and_a_name()
    {
        var client = new RecordingClient();
        await using var context = new SmokeContext(Options(client));

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        Assert.True(transaction.SupportsSavepoints);
        await transaction.CreateSavepointAsync("sp1");
        await transaction.RollbackToSavepointAsync("sp1");
        await transaction.ReleaseSavepointAsync("sp1");

        // A savepoint is not a scope of its own, so it carries no token of its own: the
        // transaction's token plus a name is the whole address.
        Assert.Equal(
            [
                "create:server-token-1:sp1",
                "rollback:server-token-1:sp1",
                "release:server-token-1:sp1"
            ],
            client.Savepoints);
    }

    private static DbContextOptions<SmokeContext> Options(IInfoCarrierClient client)
        => new DbContextOptionsBuilder<SmokeContext>().UseInfoCarrier(client).Options;

    /// <summary>
    ///     A client that records what crossed, and answers a save with "nothing generated".
    /// </summary>
    private sealed class RecordingClient : IInfoCarrierClient
    {
        public int Begins { get; private set; }

        public List<string?> SaveTokens { get; } = [];

        public List<string> Ends { get; } = [];

        public List<string> Savepoints { get; } = [];

        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No query is expected in these tests.");

        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
        {
            SaveTokens.Add(request.TransactionId);
            return Task.FromResult(new SaveChangesResult { Count = request.Entries.Count, GeneratedValues = [] });
        }

        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            Begins++;
            return Task.FromResult(new TransactionResult { TransactionId = $"server-token-{Begins}" });
        }

        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            Ends.Add($"commit:{transactionId}");
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            Ends.Add($"rollback:{transactionId}");
            return Task.CompletedTask;
        }

        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        {
            Savepoints.Add($"create:{transactionId}:{name}");
            return Task.CompletedTask;
        }

        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        {
            Savepoints.Add($"rollback:{transactionId}:{name}");
            return Task.CompletedTask;
        }

        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        {
            Savepoints.Add($"release:{transactionId}:{name}");
            return Task.CompletedTask;
        }

        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
