// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     A transaction token names the server instance that minted it (#54, part 3).
/// </summary>
/// <remarks>
///     <para>
///         <b>THE DEFECT THESE PIN IS A WRONG DIAGNOSIS, not a wrong result.</b> The registry of
///         open transactions is a field of one <c>InProcessInfoCarrierServer</c>, so a token only
///         resolves on the process that made it. Behind a load balancer, a client that opens a
///         transaction on one instance and saves on another is refused, which is correct. What was
///         wrong is what it was told: the refusal listed "committed, rolled back, or belongs to a
///         different server", and a reader takes the first branch and hunts for a bug in their own
///         code while the truth is that they reached the wrong machine.
///     </para>
///     <para>
///         <b>A server restart produced the same wrong answer</b>, and the fix covers it for free:
///         a restarted process is a new instance, so every token minted before it is now correctly
///         reported as another instance's rather than as ended work.
///     </para>
///     <para>
///         <b>THIS DOES NOT ROUTE ANYTHING, and the tests do not pretend otherwise.</b> A live
///         store connection cannot move between processes, so session affinity is still required.
///         What changes is that the failure names its cause.
///     </para>
///     <para>
///         <b>The wire format is unchanged.</b> The token has always been an opaque string the
///         server mints and the client echoes, so putting the instance inside it breaks no
///         protocol, which is what separates this from #54's part 2.
///     </para>
/// </remarks>
public class TransactionTokenTest
{
    [Fact]
    public async Task A_token_names_the_instance_that_minted_it()
    {
        await using InProcessInfoCarrierServer server = CreateServer();

        TransactionResult begun = await server.BeginTransactionAsync();

        string[] parts = begun.TransactionId.Split('.');
        Assert.Equal(2, parts.Length);
        Assert.NotEmpty(parts[0]);
        Assert.NotEmpty(parts[1]);
    }

    /// <summary>
    ///     Two servers in one process stand in for two instances behind a load balancer.
    /// </summary>
    [Fact]
    public async Task Two_instances_mint_tokens_with_different_prefixes()
    {
        await using InProcessInfoCarrierServer a = CreateServer();
        await using InProcessInfoCarrierServer b = CreateServer();

        TransactionResult fromA = await a.BeginTransactionAsync();
        TransactionResult fromB = await b.BeginTransactionAsync();

        Assert.NotEqual(InstanceOf(fromA.TransactionId), InstanceOf(fromB.TransactionId));
    }

    /// <summary>
    ///     The case this whole step exists for: the right token, the wrong machine.
    /// </summary>
    [Fact]
    public async Task A_token_from_another_instance_is_reported_as_a_routing_problem()
    {
        await using InProcessInfoCarrierServer a = CreateServer();
        await using InProcessInfoCarrierServer b = CreateServer();

        TransactionResult begun = await a.BeginTransactionAsync();

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => b.SupportsSavepointsAsync(begun.TransactionId));

        // It names both instances, so a reader can tell the two apart in a log.
        Assert.Contains(InstanceOf(begun.TransactionId), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("different server instance", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("session affinity", thrown.Message, StringComparison.Ordinal);

        // And it does NOT offer the diagnosis that sent readers looking in their own code.
        Assert.DoesNotContain("committed", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_commit_of_another_instances_token_is_refused_the_same_way()
    {
        await using InProcessInfoCarrierServer a = CreateServer();
        await using InProcessInfoCarrierServer b = CreateServer();

        TransactionResult begun = await a.BeginTransactionAsync();

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => b.CommitTransactionAsync(begun.TransactionId));

        Assert.Contains("different server instance", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("session affinity", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A rollback stays silent even for another instance's token, which is Z1's rule and not a
    ///     new one.
    /// </summary>
    /// <remarks>
    ///     <c>InfoCarrierTransaction.DisposeAsync</c> sends a rollback unconditionally, so the
    ///     implicit end of every <c>using</c> block is a rollback. If a misrouted dispose threw,
    ///     the ordinary pattern would fail on the way out rather than on the way in, and the
    ///     original error would be lost behind it.
    /// </remarks>
    [Fact]
    public async Task A_rollback_of_another_instances_token_still_stays_silent()
    {
        await using InProcessInfoCarrierServer a = CreateServer();
        await using InProcessInfoCarrierServer b = CreateServer();

        TransactionResult begun = await a.BeginTransactionAsync();

        await b.RollbackTransactionAsync(begun.TransactionId);
    }

    /// <summary>
    ///     The control: a token this instance really did mint, and really did end.
    /// </summary>
    /// <remarks>
    ///     <b>Without this the fix could be "always blame routing", which would move the wrong
    ///     diagnosis rather than remove it.</b>
    /// </remarks>
    [Fact]
    public async Task A_token_this_instance_ended_is_not_blamed_on_routing()
    {
        await using InProcessInfoCarrierServer server = CreateServer();

        TransactionResult begun = await server.BeginTransactionAsync();
        await server.CommitTransactionAsync(begun.TransactionId);

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.SupportsSavepointsAsync(begun.TransactionId));

        Assert.Contains("committed", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("different server instance", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A token no server minted carries no instance, so there is nothing to blame routing for.
    /// </summary>
    [Fact]
    public async Task A_token_that_names_no_instance_is_not_blamed_on_routing()
    {
        await using InProcessInfoCarrierServer server = CreateServer();

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.SupportsSavepointsAsync("not-a-token-this-server-ever-issued"));

        Assert.DoesNotContain("different server instance", thrown.Message, StringComparison.Ordinal);
    }

    private static string InstanceOf(string transactionId)
        => transactionId.Split('.')[0];

    private static InProcessInfoCarrierServer CreateServer()
    {
        IServiceCollection services = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .AddDbContext<TokenContext>(b => b
                .UseInMemoryDatabase(Guid.NewGuid().ToString())

                // EF's InMemory provider raises `TransactionIgnoredWarning` as an error, because it
                // has no transactions and says so rather than pretending. What is under test here
                // is which TOKEN the registry accepts, so a stub transaction is enough.
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)))
            .AddScoped<DbContext>(sp => sp.GetRequiredService<TokenContext>());

        return new InProcessInfoCarrierServer(services.BuildServiceProvider(validateScopes: true));
    }

    /// <summary>
    ///     One entity, because these tests are about the token rather than about a model.
    /// </summary>
    public class TokenContext(DbContextOptions<TokenContext> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }

    /// <summary>
    ///     The one entity <see cref="TokenContext" /> maps.
    /// </summary>
    public class Row
    {
        public int Id { get; set; }
    }
}
