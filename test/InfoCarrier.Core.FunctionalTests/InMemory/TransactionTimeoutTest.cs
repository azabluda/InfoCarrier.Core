// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     The idle timeout that evicts a server-held transaction whose client has vanished (#54).
/// </summary>
/// <remarks>
///     <para>
///         <b>The defect these pin.</b> <c>InProcessInfoCarrierServer</c> keeps an open
///         transaction, and the DI scope, <c>DbContext</c> and store connection it holds, in a
///         dictionary keyed by the wire token. Only a commit or a rollback removes an entry, and
///         the client's own <c>DisposeAsync</c> cannot run for a client that never runs again: a
///         closed tab, a dropped network, a crashed process. Once such a transaction has written
///         it holds the store's write lock until the server process exits.
///     </para>
///     <para>
///         <b>OFF UNLESS REGISTERED, which is what the third test pins.</b> A server that does not
///         call <c>AddInfoCarrierServerTransactionTimeout</c> behaves exactly as it did in
///         <c>10.1.0</c>, so upgrading changes nothing. The grant follows the same shape as every
///         other server-side one: a marker read from the root provider.
///     </para>
///     <para>
///         <b>Nothing here sleeps, and that is the whole reason <c>FakeTimeProvider</c> is
///         referenced.</b> <c>Advance</c> fires every timer callback that has come due,
///         synchronously on the calling thread. A hand-rolled <c>TimeProvider</c> overriding only
///         <c>GetUtcNow()</c> was measured and rejected: the base <c>TimeProvider.CreateTimer</c>
///         stays on the system clock, so such a test passes only when the real period happens to
///         elapse inside the assertion's wait.
///     </para>
///     <para>
///         The server is built by hand rather than through <c>InfoCarrierTestStoreFactory</c>, as
///         <c>InMemorySmokeTest</c> builds its own: what is under test is the server's own
///         registry, and a hand-built collection is the shape a consumer application has.
///     </para>
/// </remarks>
public class TransactionTimeoutTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task An_idle_transaction_is_evicted_and_rolled_back()
    {
        var clock = new FakeTimeProvider();
        await using InProcessInfoCarrierServer server = CreateServer(clock, Timeout);

        TransactionResult begun = await server.BeginTransactionAsync();

        // Idle past the timeout. `Advance` runs the sweep the periodic timer drives, on this
        // thread, so there is nothing to wait for and nothing to race.
        clock.Advance(Timeout + TimeSpan.FromMinutes(1));

        // The token is gone. Asserted through an operation that LOOKS THE TOKEN UP, because
        // `CommitTransactionAsync` deliberately does not: `EndAsync` returns quietly when the
        // entry has already gone, so that a client rolling back on disposal after an explicit
        // commit is not punished for following the ordinary `using` pattern. Every operation that
        // does real work goes through `Open`, and that is what a returning client meets.
        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.SupportsSavepointsAsync(begun.TransactionId));

        Assert.Contains("is not open on this server", thrown.Message);
    }

    [Fact]
    public async Task Committing_after_an_eviction_throws_rather_than_reporting_success()
    {
        // THE HAZARD THIS CLOSES, and the first design of it had the answer backwards. A client
        // that did real work and then went idle is NOT told at its next request: its next request
        // IS the commit, and commit is the one operation that never looks the token up. Returning
        // quietly would report success for writes the server had already rolled back.
        var clock = new FakeTimeProvider();
        await using InProcessInfoCarrierServer server = CreateServer(clock, Timeout);

        TransactionResult begun = await server.BeginTransactionAsync();

        // Real work inside the transaction, which also refreshes liveness.
        await server.SupportsSavepointsAsync(begun.TransactionId);

        clock.Advance(Timeout + TimeSpan.FromMinutes(1));

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.CommitTransactionAsync(begun.TransactionId));

        Assert.Contains("cannot be", thrown.Message);
        Assert.Contains("discarded", thrown.Message);
    }

    [Fact]
    public async Task Rolling_back_after_an_eviction_stays_silent()
    {
        // THE OTHER HALF, and it is what keeps the `using` pattern working. A client disposes its
        // transaction after an explicit commit, which rolls back; EF's own transaction objects
        // tolerate that and so must this. Only the COMMIT direction is loud.
        var clock = new FakeTimeProvider();
        await using InProcessInfoCarrierServer server = CreateServer(clock, Timeout);

        TransactionResult begun = await server.BeginTransactionAsync();

        clock.Advance(Timeout + TimeSpan.FromMinutes(1));

        await server.RollbackTransactionAsync(begun.TransactionId);
    }

    [Fact]
    public async Task Activity_refreshes_liveness_so_a_working_transaction_survives()
    {
        var clock = new FakeTimeProvider();
        await using InProcessInfoCarrierServer server = CreateServer(clock, Timeout);

        TransactionResult begun = await server.BeginTransactionAsync();

        // Three quarters of the timeout, one request, then three quarters again. The total elapsed
        // time is past the timeout, so without the refresh this transaction would be gone.
        clock.Advance(Timeout * 0.75);
        await server.SupportsSavepointsAsync(begun.TransactionId);
        clock.Advance(Timeout * 0.75);

        // Still open, so committing succeeds rather than throwing.
        await server.CommitTransactionAsync(begun.TransactionId);
    }

    [Fact]
    public async Task Without_the_grant_nothing_is_ever_evicted()
    {
        // THE PIN ON THE DEFAULT. A server that says nothing holds its transaction for as long as
        // the process lives, which is the 10.1.0 behaviour and is what makes this opt-in rather
        // than a change every consumer inherits on upgrade.
        var clock = new FakeTimeProvider();
        await using InProcessInfoCarrierServer server = CreateServer(clock, idleTimeout: null);

        TransactionResult begun = await server.BeginTransactionAsync();

        clock.Advance(TimeSpan.FromDays(7));

        await server.CommitTransactionAsync(begun.TransactionId);
    }

    [Fact]
    public async Task Disposing_the_server_stops_the_sweep_and_is_idempotent()
    {
        var clock = new FakeTimeProvider();
        InProcessInfoCarrierServer server = CreateServer(clock, Timeout);

        await server.BeginTransactionAsync();

        await server.DisposeAsync();
        await server.DisposeAsync();

        // Advancing past the timeout after disposal must not run a sweep against a disposed
        // server. Nothing is asserted about the transaction; what is under test is that this does
        // not throw.
        clock.Advance(Timeout * 2);
    }

    /// <summary>
    ///     Every operation that names a token, against a token the server no longer holds.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>THE RULE THIS ASSERTS, and it is one rule rather than a list:</b> a token the
    ///         server does not hold is refused by every operation except a rollback, which stays
    ///         silent. Nothing else is special-cased.
    ///     </para>
    ///     <para>
    ///         <b>Rollback is the exception because of the pattern it serves</b>, not because
    ///         rollback is unimportant: a client that commits and then disposes sends a rollback,
    ///         and EF's own transaction objects tolerate exactly that. That pattern never ends in a
    ///         second commit, which is what lets the two directions differ with no record of ended
    ///         tokens.
    ///     </para>
    ///     <para>
    ///         <c>QueryDataAsync</c> is absent because it needs a serialized tree; it reaches the
    ///         same lookup through <c>Acquire</c> that <c>SaveChangesAsync</c> reaches, and that one
    ///         is here.
    ///     </para>
    ///     <para>
    ///         <b>THE LAMBDA BELOW IS LOAD-BEARING, and passing an already-started task instead is
    ///         what four of these seven cases first failed on.</b> The refusal is delivered two
    ///         different ways: the four savepoint operations are expression-bodied and non-async,
    ///         so <c>Open</c> throws before a task exists and the exception arrives
    ///         SYNCHRONOUSLY, while a save, a commit and a rollback run inside an async state
    ///         machine and hand back a FAULTED TASK. <c>Assert.ThrowsAsync</c> catches the second
    ///         either way and the first only when it does the calling.
    ///     </para>
    ///     <para>
    ///         <b>That split is not a defect and this test deliberately does not assert it away.</b>
    ///         <c>InfoCarrierEnvelopeServer.ExecuteAsync</c> is one async method wrapping the whole
    ///         operation switch, so its state machine captures a synchronous throw exactly as it
    ///         captures a faulted task, and both reach the client as the same error envelope. No
    ///         caller can tell them apart, so the rule worth pinning is that the operation is
    ///         REFUSED, not how the refusal travels.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("save", true)]
    [InlineData("createSavepoint", true)]
    [InlineData("rollbackToSavepoint", true)]
    [InlineData("releaseSavepoint", true)]
    [InlineData("supportsSavepoints", true)]
    [InlineData("commit", true)]
    [InlineData("rollback", false)]
    public async Task An_operation_on_an_evicted_token_is_refused_unless_it_is_a_rollback(
        string operation, bool expectThrow)
    {
        var clock = new FakeTimeProvider();
        await using InProcessInfoCarrierServer server = CreateServer(clock, Timeout);

        TransactionResult begun = await server.BeginTransactionAsync();
        clock.Advance(Timeout + TimeSpan.FromMinutes(1));

        if (expectThrow)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Invoke(server, operation, begun.TransactionId));
        }
        else
        {
            await Invoke(server, operation, begun.TransactionId);
        }
    }

    /// <summary>
    ///     The four ways a token stops being held, against the two operations that differ.
    /// </summary>
    /// <remarks>
    ///     <b>The cause does not change the answer, which is the point of asserting it.</b> A
    ///     commit cannot tell an eviction from a double-commit and must not try: in every one of
    ///     these the work is gone, so reporting success would report it wrongly.
    /// </remarks>
    [Theory]
    [InlineData("evicted")]
    [InlineData("committed")]
    [InlineData("rolledBack")]
    [InlineData("neverExisted")]
    public async Task A_commit_throws_and_a_rollback_stays_silent_however_the_token_was_lost(string cause)
    {
        var clock = new FakeTimeProvider();
        await using InProcessInfoCarrierServer server = CreateServer(clock, Timeout);

        string token = await LoseTokenAsync(server, clock, cause);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.CommitTransactionAsync(token));

        // And the same token, rolled back, says nothing.
        await server.RollbackTransactionAsync(token);
    }

    /// <summary>
    ///     A live transaction answers every one of the same operations without complaint.
    /// </summary>
    /// <remarks>
    ///     <b>The control, and the tests above are worth little without it.</b> A rule that refuses
    ///     everything would satisfy them. Savepoint CREATION is not here: EF's InMemory transaction
    ///     is a stub and refuses one on its own account, which is the store's answer rather than
    ///     the registry's.
    /// </remarks>
    [Fact]
    public async Task A_live_token_is_accepted_by_the_operations_that_look_it_up()
    {
        var clock = new FakeTimeProvider();
        await using InProcessInfoCarrierServer server = CreateServer(clock, Timeout);

        TransactionResult begun = await server.BeginTransactionAsync();

        await server.SupportsSavepointsAsync(begun.TransactionId);
        await server.SaveChangesAsync(
            new SaveChangesRequest { Entries = [], TransactionId = begun.TransactionId });

        await server.CommitTransactionAsync(begun.TransactionId);
    }

    private static async Task<string> LoseTokenAsync(
        InProcessInfoCarrierServer server, FakeTimeProvider clock, string cause)
    {
        if (cause == "neverExisted")
        {
            return Guid.NewGuid().ToString("N");
        }

        TransactionResult begun = await server.BeginTransactionAsync();

        switch (cause)
        {
            case "evicted":
                clock.Advance(Timeout + TimeSpan.FromMinutes(1));
                break;
            case "committed":
                await server.CommitTransactionAsync(begun.TransactionId);
                break;
            case "rolledBack":
                await server.RollbackTransactionAsync(begun.TransactionId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cause), cause, "unknown cause");
        }

        return begun.TransactionId;
    }

    private static Task Invoke(InProcessInfoCarrierServer server, string operation, string token)
        => operation switch
        {
            "save" => server.SaveChangesAsync(
                new SaveChangesRequest { Entries = [], TransactionId = token }),
            "createSavepoint" => server.CreateSavepointAsync(token, "sp"),
            "rollbackToSavepoint" => server.RollbackToSavepointAsync(token, "sp"),
            "releaseSavepoint" => server.ReleaseSavepointAsync(token, "sp"),
            "supportsSavepoints" => server.SupportsSavepointsAsync(token),
            "commit" => server.CommitTransactionAsync(token),
            "rollback" => server.RollbackTransactionAsync(token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "unknown operation"),
        };

    private static InProcessInfoCarrierServer CreateServer(TimeProvider clock, TimeSpan? idleTimeout)
    {
        IServiceCollection services = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .AddDbContext<TimeoutContext>(b => b
                .UseInMemoryDatabase(Guid.NewGuid().ToString())

                // EF's InMemory provider raises `TransactionIgnoredWarning` AS AN ERROR from
                // `BeginTransactionAsync`, because it has no transactions and says so rather than
                // pretending. That is the right behaviour and the server relays it, which is why
                // these tests have to suppress it to reach the thing they are about.
                //
                // A STUB TRANSACTION IS ENOUGH HERE, because what is under test is the server's
                // REGISTRY: that an entry is stamped, refreshed, evicted or left alone. Whether
                // the store's rollback does anything is the store's business, and the eviction
                // path calls it either way.
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)))
            .AddScoped<DbContext>(sp => sp.GetRequiredService<TimeoutContext>())
            .AddSingleton(clock);

        if (idleTimeout is { } timeout)
        {
            services.AddInfoCarrierServerTransactionTimeout(timeout);
        }

        return new InProcessInfoCarrierServer(services.BuildServiceProvider(validateScopes: true));
    }

    /// <summary>
    ///     One entity, because these tests are about the registry rather than about a model.
    /// </summary>
    public class TimeoutContext(DbContextOptions<TimeoutContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }

    /// <summary>
    ///     The one entity <see cref="TimeoutContext" /> maps.
    /// </summary>
    public class Row
    {
        public int Id { get; set; }
    }
}
