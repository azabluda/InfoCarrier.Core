// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     A commit or a rollback whose request or answer the transport loses is not treated as done.
/// </summary>
/// <remarks>
///     <para>
///         <b>The defect these pin, found 2026-09-29 while designing #48.</b>
///         <c>InfoCarrierTransaction</c> marked itself finished BEFORE the round trip, so a transport
///         failure left it finished anyway. A second <c>Commit</c> then returned at once and reported
///         success without reaching the server, and the <c>using</c> block's dispose sent no rollback:
///         a commit that never arrived left the server holding the transaction, its connection and
///         its locks, until the idle timeout or, with none registered, until the process exited.
///         EF's own <c>RelationalTransaction</c> clears its state only after the store answered.
///     </para>
///     <para>
///         <b>No test could see it before</b>: every transport in this suite delivers every request
///         and every answer, and EF tests a failed commit only in its SQL Server suite, with a fake
///         connection, which is not a base this repository inherits. <see cref="LosingTransport" />
///         loses one request or one answer on purpose.
///     </para>
///     <para>
///         The server is InMemory and built by hand, as <see cref="TransactionTimeoutTest" /> builds
///         it, because what is asserted is the server's registry of open transactions: whether the
///         token is still held. <c>SupportsSavepointsAsync</c> is the probe because it looks the
///         token up, which a commit or a rollback of an unknown token deliberately does not.
///     </para>
/// </remarks>
public class LostTransactionEndTest
{
    [Fact]
    public async Task A_commit_lost_on_the_way_out_is_sent_again_by_the_next_commit()
    {
        await using var harness = new Harness();

        await using IDbContextTransaction transaction = await harness.Context.Database.BeginTransactionAsync();
        string token = ((InfoCarrierTransaction)transaction).ServerTransactionId;

        harness.Transport.Lose(InfoCarrierOperation.CommitTransaction, beforeTheServer: true);
        await Assert.ThrowsAsync<InfoCarrierTransportException>(() => transaction.CommitAsync());
        Assert.True(await harness.IsOpenAsync(token), "the lost commit must not have reached the server");

        await transaction.CommitAsync();

        Assert.False(await harness.IsOpenAsync(token), "the second commit did not reach the server");
    }

    [Fact]
    public async Task A_commit_whose_answer_was_lost_is_not_reported_as_success_by_the_next_commit()
    {
        await using var harness = new Harness();

        await using IDbContextTransaction transaction = await harness.Context.Database.BeginTransactionAsync();

        harness.Transport.Lose(InfoCarrierOperation.CommitTransaction, beforeTheServer: false);
        await Assert.ThrowsAsync<InfoCarrierTransportException>(() => transaction.CommitAsync());

        // The server did commit. The client cannot know that, so it must not claim either outcome:
        // the second commit reaches a server that no longer holds the token, and that answer is
        // an exception, never a quiet success.
        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => transaction.CommitAsync());
        Assert.Contains("is not open on this server", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_commit_lost_on_the_way_out_is_rolled_back_on_the_server_by_the_dispose()
    {
        await using var harness = new Harness();

        string token;
        await using (IDbContextTransaction transaction = await harness.Context.Database.BeginTransactionAsync())
        {
            token = ((InfoCarrierTransaction)transaction).ServerTransactionId;

            harness.Transport.Lose(InfoCarrierOperation.CommitTransaction, beforeTheServer: true);
            await Assert.ThrowsAsync<InfoCarrierTransportException>(() => transaction.CommitAsync());
        }

        Assert.False(await harness.IsOpenAsync(token), "the dispose left the transaction open on the server");
    }

    [Fact]
    public async Task A_rollback_lost_on_the_way_out_is_sent_again_by_the_dispose()
    {
        await using var harness = new Harness();

        string token;
        await using (IDbContextTransaction transaction = await harness.Context.Database.BeginTransactionAsync())
        {
            token = ((InfoCarrierTransaction)transaction).ServerTransactionId;

            harness.Transport.Lose(InfoCarrierOperation.RollbackTransaction, beforeTheServer: true);
            await Assert.ThrowsAsync<InfoCarrierTransportException>(() => transaction.RollbackAsync());
        }

        Assert.False(await harness.IsOpenAsync(token), "the dispose left the transaction open on the server");
    }

    /// <summary>
    ///     A hand-built InMemory server, a client context whose transport can lose one request, and
    ///     the probe that asks the server whether it still holds a token.
    /// </summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly InProcessInfoCarrierServer _server;

        public Harness()
        {
            _server = new InProcessInfoCarrierServer(
                new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .AddDbContext<TransactionTimeoutTest.TimeoutContext>(b => b
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())

                        // InMemory has no transactions and says so as an error; a stub is enough
                        // here, for the reason TransactionTimeoutTest gives.
                        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)))
                    .AddScoped<DbContext>(sp => sp.GetRequiredService<TransactionTimeoutTest.TimeoutContext>())
                    .BuildServiceProvider(validateScopes: true));

            var serializer = new SystemTextJsonInfoCarrierSerializer();
            Transport = new LosingTransport(new InProcessInfoCarrierTransport(
                new InfoCarrierEnvelopeServer(_server, serializer).DispatchAsync, serializer));

            Context = new TransactionTimeoutTest.TimeoutContext(
                new DbContextOptionsBuilder<TransactionTimeoutTest.TimeoutContext>()
                    .UseInfoCarrier(new TransportInfoCarrierClient(Transport, serializer))
                    .Options);
        }

        public LosingTransport Transport { get; }

        public TransactionTimeoutTest.TimeoutContext Context { get; }

        public async Task<bool> IsOpenAsync(string token)
        {
            try
            {
                await _server.SupportsSavepointsAsync(token);
                return true;
            }
            catch (InvalidOperationException ex)
                when (ex.Message.Contains("is not open on this server", StringComparison.Ordinal))
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _server.DisposeAsync();
        }
    }

    /// <summary>
    ///     Delivers everything, except one request of the named operation, which it loses either
    ///     before the server sees it or after the server answered.
    /// </summary>
    private sealed class LosingTransport(IInfoCarrierTransport inner) : IInfoCarrierTransport
    {
        private InfoCarrierOperation? _operation;
        private bool _beforeTheServer;

        public void Lose(InfoCarrierOperation operation, bool beforeTheServer)
        {
            _operation = operation;
            _beforeTheServer = beforeTheServer;
        }

        public async Task<InfoCarrierEnvelope> SendAsync(
            InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
        {
            if (request.Operation != _operation)
            {
                return await inner.SendAsync(request, cancellationToken);
            }

            _operation = null;

            if (!_beforeTheServer)
            {
                await inner.SendAsync(request, cancellationToken);
            }

            throw new InfoCarrierTransportException(
                $"LosingTransport lost the {request.Operation} "
                + (_beforeTheServer ? "request before the server saw it." : "answer after the server sent it."));
        }
    }
}
