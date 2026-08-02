// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     The provider does not remote transactions yet (roadmap M4). It reports that the way EF
///     Core's InMemory provider reports the same thing: the operation is <em>ignored</em>, and
///     <see cref="InfoCarrierEventId.TransactionIgnoredWarning" /> is a warning-as-error unless
///     the caller opts into the no-op.
/// </summary>
/// <remarks>
///     These assertions exist because the change that introduced the stub moved no test count at
///     all — every mutation spec base was already unreachable for other reasons — so nothing in a
///     suite run distinguishes "the stub works" from "the stub is never called".
/// </remarks>
public class TransactionIgnoredTest
{
    [Fact]
    public void BeginTransaction_throws_by_default_rather_than_pretending()
    {
        using var context = new SmokeContext(Options(optIn: false));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => context.Database.BeginTransaction());

        Assert.Contains("InfoCarrierEventId.TransactionIgnoredWarning", exception.Message);
    }

    [Fact]
    public async Task BeginTransactionAsync_throws_by_default_rather_than_pretending()
    {
        using var context = new SmokeContext(Options(optIn: false));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Database.BeginTransactionAsync());
    }

    [Fact]
    public void An_opted_in_caller_gets_a_stub_that_commits_and_rolls_back_silently()
    {
        using var context = new SmokeContext(Options(optIn: true));

        using IDbContextTransaction transaction = context.Database.BeginTransaction();

        Assert.NotEqual(Guid.Empty, transaction.TransactionId);
        transaction.Commit();
        transaction.Rollback();
    }

    [Fact]
    public void The_ignored_transaction_is_never_the_current_transaction()
    {
        using var context = new SmokeContext(Options(optIn: true));

        using IDbContextTransaction transaction = context.Database.BeginTransaction();

        // A stub that reported itself as current would let EF believe the next SaveChanges is
        // enlisted in something.
        Assert.Null(context.Database.CurrentTransaction);
    }

    private static DbContextOptions<SmokeContext> Options(bool optIn)
    {
        var builder = new DbContextOptionsBuilder<SmokeContext>()
            .UseInfoCarrier(new UnreachableClient());

        if (optIn)
        {
            builder = builder.ConfigureWarnings(
                w => w.Log(InfoCarrierEventId.TransactionIgnoredWarning));
        }

        return builder.Options;
    }

    /// <summary>
    ///     A client that fails the test if anything reaches the server. Beginning a transaction
    ///     must not produce a round trip while the operation is being ignored.
    /// </summary>
    private sealed class UnreachableClient : IInfoCarrierClient
    {
        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The server was contacted.");

        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The server was contacted.");

        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The server was contacted.");

        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The server was contacted.");

        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The server was contacted.");
    }
}
