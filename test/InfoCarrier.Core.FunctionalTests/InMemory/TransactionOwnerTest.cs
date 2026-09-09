// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     A server may bind a transaction to the caller who opened it (#54, part 2).
/// </summary>
/// <remarks>
///     <para>
///         <b>THE DEFECT THESE PIN IS THAT A TOKEN IS THE ONLY CREDENTIAL.</b> Anyone holding one
///         can use the transaction it names, and the server hands back the OPENER'S
///         <c>DbContext</c>, on the opener's connection, inside the opener's transaction. So the
///         exposure is not merely that a stranger can end somebody else's unit of work: they can
///         query and save inside it first, and then commit it.
///     </para>
///     <para>
///         <b>Nothing on the wire changes, which is why this was possible without a protocol
///         break.</b> The identity is OBSERVED by an authenticated transport rather than asserted
///         by the client, so the envelope is untouched and an old client works unmodified.
///     </para>
/// </remarks>
public class TransactionOwnerTest
{
    /// <summary>
    ///     Off unless registered, which is what keeps this from breaking a working deployment.
    /// </summary>
    [Fact]
    public async Task Without_the_grant_any_caller_may_use_the_token()
    {
        await using InProcessInfoCarrierServer server = CreateServer(identity: null);

        TransactionResult begun = await server.BeginTransactionAsync();

        // No identity is registered, so nothing distinguishes callers and the token is the whole
        // credential. This is 10.1.0's behaviour and upgrading must not change it.
        await server.SupportsSavepointsAsync(begun.TransactionId);
        await server.CommitTransactionAsync(begun.TransactionId);
    }

    [Fact]
    public async Task The_caller_who_opened_it_is_accepted()
    {
        var caller = new MutableCaller("alice");
        await using InProcessInfoCarrierServer server = CreateServer(caller);

        TransactionResult begun = await server.BeginTransactionAsync();

        await server.SupportsSavepointsAsync(begun.TransactionId);
        await server.SaveChangesAsync(
            new SaveChangesRequest { Entries = [], TransactionId = begun.TransactionId });
        await server.CommitTransactionAsync(begun.TransactionId);
    }

    /// <summary>
    ///     The attack: a token taken from somebody else's screen.
    /// </summary>
    [Theory]
    [InlineData("save")]
    [InlineData("supportsSavepoints")]
    [InlineData("createSavepoint")]
    [InlineData("commit")]
    [InlineData("rollback")]
    public async Task A_different_caller_is_refused_by_every_operation(string operation)
    {
        var caller = new MutableCaller("alice");
        await using InProcessInfoCarrierServer server = CreateServer(caller);

        TransactionResult begun = await server.BeginTransactionAsync();
        caller.Id = "bob";

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Invoke(server, operation, begun.TransactionId));

        Assert.Contains("a different caller", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     <b>A ROLLBACK IS REFUSED HERE AND TOLERATED ELSEWHERE, and the difference is the
    ///     point.</b>
    /// </summary>
    /// <remarks>
    ///     Z1 made a rollback naming a token the server does NOT hold stay silent, because
    ///     <c>InfoCarrierTransaction.DisposeAsync</c> sends one unconditionally and the implicit
    ///     end of every <c>using</c> block would otherwise throw. That reasoning does not reach
    ///     this case: the server DOES hold the transaction, it belongs to somebody else, and
    ///     destroying their work is exactly the attack. So the token being held by another caller
    ///     is refused where the token being absent is not.
    /// </remarks>
    [Fact]
    public async Task A_stranger_cannot_destroy_the_work_by_rolling_it_back()
    {
        var caller = new MutableCaller("alice");
        await using InProcessInfoCarrierServer server = CreateServer(caller);

        TransactionResult begun = await server.BeginTransactionAsync();
        caller.Id = "bob";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.RollbackTransactionAsync(begun.TransactionId));

        // And the refusal left it open, so its owner can still finish it.
        caller.Id = "alice";
        await server.CommitTransactionAsync(begun.TransactionId);
    }

    /// <summary>
    ///     The owner's own disposal still works, which is the pattern Z1 protected.
    /// </summary>
    [Fact]
    public async Task The_owner_can_still_roll_back_on_disposal()
    {
        var caller = new MutableCaller("alice");
        await using InProcessInfoCarrierServer server = CreateServer(caller);

        TransactionResult begun = await server.BeginTransactionAsync();

        await server.CommitTransactionAsync(begun.TransactionId);

        // What `DisposeAsync` sends after a commit: a rollback for a token that is now gone.
        await server.RollbackTransactionAsync(begun.TransactionId);
    }

    /// <summary>
    ///     The refusal must not tell a thief whose transaction they found.
    /// </summary>
    [Fact]
    public async Task The_refusal_does_not_name_the_caller_who_opened_it()
    {
        var caller = new MutableCaller("alice@example.com");
        await using InProcessInfoCarrierServer server = CreateServer(caller);

        TransactionResult begun = await server.BeginTransactionAsync();
        caller.Id = "bob";

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.SupportsSavepointsAsync(begun.TransactionId));

        Assert.DoesNotContain("alice", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     A null caller binds like any other value.
    /// </summary>
    /// <remarks>
    ///     It cannot separate two anonymous callers, because nothing distinguishes them. What it
    ///     does do is stop an authenticated caller from picking up a transaction opened
    ///     anonymously, and the reverse.
    /// </remarks>
    [Fact]
    public async Task An_anonymous_transaction_is_not_available_to_a_named_caller()
    {
        var caller = new MutableCaller(null);
        await using InProcessInfoCarrierServer server = CreateServer(caller);

        TransactionResult begun = await server.BeginTransactionAsync();

        caller.Id = "alice";
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.SupportsSavepointsAsync(begun.TransactionId));

        caller.Id = null;
        await server.CommitTransactionAsync(begun.TransactionId);
    }

    private static Task Invoke(InProcessInfoCarrierServer server, string operation, string token)
        => operation switch
        {
            "save" => server.SaveChangesAsync(
                new SaveChangesRequest { Entries = [], TransactionId = token }),
            "supportsSavepoints" => server.SupportsSavepointsAsync(token),
            "createSavepoint" => server.CreateSavepointAsync(token, "sp"),
            "commit" => server.CommitTransactionAsync(token),
            "rollback" => server.RollbackTransactionAsync(token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "unknown operation"),
        };

    private static InProcessInfoCarrierServer CreateServer(IInfoCarrierServerCallerIdentity? identity)
    {
        IServiceCollection services = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .AddDbContext<OwnerContext>(b => b
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)))
            .AddScoped<DbContext>(sp => sp.GetRequiredService<OwnerContext>());

        if (identity is not null)
        {
            services.AddSingleton(identity);
        }

        return new InProcessInfoCarrierServer(services.BuildServiceProvider(validateScopes: true));
    }

    /// <summary>
    ///     Stands in for an authenticated transport, so a test can change who is calling between
    ///     one request and the next without an HTTP stack.
    /// </summary>
    private sealed class MutableCaller(string? id) : IInfoCarrierServerCallerIdentity
    {
        public string? Id { get; set; } = id;

        public string? CurrentCallerId => this.Id;
    }

    /// <summary>
    ///     One entity, because these tests are about the owner rather than about a model.
    /// </summary>
    public class OwnerContext(DbContextOptions<OwnerContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }

    /// <summary>
    ///     The one entity <see cref="OwnerContext" /> maps.
    /// </summary>
    public class Row
    {
        public int Id { get; set; }
    }
}
