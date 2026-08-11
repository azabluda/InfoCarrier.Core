// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Expressions;

/// <summary>
///     Cancellation across the wire (wire-protocol W6, milestone M5).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this exists, and it is C45's finding in a second place.</b> The
///         <see cref="CancellationToken" /> is threaded through every layer of the product —
///         <c>QueryContext.CancellationToken</c> → <see cref="IInfoCarrierClient" /> →
///         <see cref="TransportInfoCarrierClient" /> → <see cref="IInfoCarrierTransport" /> →
///         <see cref="InfoCarrierEnvelopeServer.DispatchAsync" /> → every
///         <see cref="IInfoCarrierServer" /> method. Every one of those signatures takes a token
///         and passes it on. **Nothing asserted that any of them did**, and a parameter carried
///         and never checked is the same class of thing as a version field nobody reads.
///     </para>
///     <para>
///         The gap that hid it was in the harness rather than the product:
///         <c>InfoCarrierBackendTestStore</c>'s transport took a handler of
///         <c>Func&lt;InfoCarrierEnvelope, Task&lt;InfoCarrierEnvelope&gt;&gt;</c> and dropped the
///         token on the floor, so the whole spec suite — every async query and every
///         <c>SaveChangesAsync</c> in it — ran with the server receiving
///         <see cref="CancellationToken.None" /> no matter what the caller passed.
///     </para>
/// </remarks>
public class CancellationTest
{
    private static readonly SystemTextJsonInfoCarrierSerializer Serializer = new();

    private static InfoCarrierEnvelope Envelope(InfoCarrierOperation operation, object? payload = null)
        => new()
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = operation,
            Payload = Serializer.Serialize(payload),
        };

    /// <summary>
    ///     Every operation the enum declares hands the caller's own token to the server — not a
    ///     copy, not <see cref="CancellationToken.None" />.
    /// </summary>
    /// <remarks>
    ///     Identity is asserted twice over, because equality alone would also hold for two default
    ///     tokens: the recorded token must equal the caller's, <b>and</b> cancelling the caller's
    ///     source afterwards must be visible through every recorded one. A default token can
    ///     satisfy neither.
    /// </remarks>
    [Fact]
    public async Task Every_operation_hands_the_callers_token_to_the_server()
    {
        var recorder = new TokenRecordingServer();
        var server = new InfoCarrierEnvelopeServer(recorder, Serializer);
        using var cts = new CancellationTokenSource();

        await server.DispatchAsync(
            Envelope(
                InfoCarrierOperation.Query,
                new QueryDataRequest
                {
                    SerializedQuery = Serializer.Serialize(1),
                    TrackingBehavior = QueryTrackingBehavior.TrackAll,
                    IsAsync = true,
                    ReturnsSingleResult = false,
                }),
            cts.Token);
        await server.DispatchAsync(
            Envelope(InfoCarrierOperation.SaveChanges, new SaveChangesRequest { Entries = [] }), cts.Token);
        await server.DispatchAsync(Envelope(InfoCarrierOperation.BeginTransaction), cts.Token);
        await server.DispatchAsync(Envelope(InfoCarrierOperation.CommitTransaction, "t1"), cts.Token);
        await server.DispatchAsync(Envelope(InfoCarrierOperation.RollbackTransaction, "t2"), cts.Token);
        await server.DispatchAsync(
            Envelope(InfoCarrierOperation.CreateSavepoint, new SavepointRequest { TransactionId = "t", Name = "s" }),
            cts.Token);
        await server.DispatchAsync(
            Envelope(InfoCarrierOperation.RollbackToSavepoint, new SavepointRequest { TransactionId = "t", Name = "s" }),
            cts.Token);
        await server.DispatchAsync(
            Envelope(InfoCarrierOperation.ReleaseSavepoint, new SavepointRequest { TransactionId = "t", Name = "s" }),
            cts.Token);
        await server.DispatchAsync(Envelope(InfoCarrierOperation.SupportsSavepoints, "t3"), cts.Token);

        // One per operation — so an operation added without a token fails here rather than at a
        // caller's first attempt to cancel it.
        Assert.Equal(Enum.GetValues<InfoCarrierOperation>().Length, recorder.Tokens.Count);
        Assert.All(recorder.Tokens, token => Assert.Equal(cts.Token, token));

        await cts.CancelAsync();
        Assert.All(recorder.Tokens, token => Assert.True(token.IsCancellationRequested));
    }

    /// <summary>
    ///     Cancellation escapes rather than travelling as a fault, and that is deliberate: it is
    ///     the caller's own token rather than a server-side failure, so the caller is entitled to
    ///     its own <see cref="OperationCanceledException" /> instead of a rebuilt copy.
    /// </summary>
    [Fact]
    public async Task A_cancelled_operation_escapes_rather_than_becoming_a_fault()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var server = new InfoCarrierEnvelopeServer(
            new ThrowingServer(new OperationCanceledException(cts.Token)), Serializer);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => server.DispatchAsync(Envelope(InfoCarrierOperation.BeginTransaction), cts.Token));
    }

    /// <summary>
    ///     The contrast that gives the test above its meaning: any <em>other</em> server-side
    ///     failure does become a fault (W5). If both escaped, the first test would be asserting
    ///     nothing about cancellation in particular.
    /// </summary>
    [Fact]
    public async Task A_failure_that_is_not_cancellation_still_becomes_a_fault()
    {
        var server = new InfoCarrierEnvelopeServer(
            new ThrowingServer(new InvalidOperationException("not a cancellation")), Serializer);

        InfoCarrierEnvelope response = await server.DispatchAsync(Envelope(InfoCarrierOperation.BeginTransaction));

        Assert.NotNull(response.Fault);
        Assert.Equal(typeof(InvalidOperationException).FullName, response.Fault!.TypeName);
    }

    /// <summary>
    ///     <see cref="InProcessInfoCarrierTransport" /> carries the token to its handler. It is
    ///     the transport the smoke test uses and the reference implementation for a real one, so
    ///     a transport that dropped the token would make every layer above it moot.
    /// </summary>
    [Fact]
    public async Task The_in_process_transport_carries_the_token()
    {
        CancellationToken received = default;
        var transport = new InProcessInfoCarrierTransport(
            (envelope, token) =>
            {
                received = token;
                return Task.FromResult(envelope);
            },
            Serializer);

        using var cts = new CancellationTokenSource();
        await transport.SendAsync(Envelope(InfoCarrierOperation.BeginTransaction), cts.Token);

        Assert.Equal(cts.Token, received);
    }

    /// <summary>
    ///     End to end through the real client, transport and server: a cancelled token cancels an
    ///     async query rather than returning rows.
    /// </summary>
    [Fact]
    public async Task A_cancelled_token_cancels_a_query_end_to_end()
    {
        await using SmokeContext context = await SeededClientAsync();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Blogs.OrderBy(b => b.Id).ToListAsync(cts.Token));
    }

    /// <summary>
    ///     And the same for the write path, which reaches the server through a different operation
    ///     and a different executor.
    /// </summary>
    [Fact]
    public async Task A_cancelled_token_cancels_SaveChanges_end_to_end()
    {
        await using SmokeContext context = await SeededClientAsync();
        context.Blogs.Add(new Blog { Id = 3, Title = "gamma" });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.SaveChangesAsync(cts.Token));
    }

    /// <summary>
    ///     A client context wired to a seeded in-process server, as <c>InMemorySmokeTest</c> does.
    /// </summary>
    private static async Task<SmokeContext> SeededClientAsync()
    {
        string databaseName = Guid.NewGuid().ToString();

        ServiceProvider serverProvider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
            .AddScoped<IExpressionSerializer, ExpressionSerializer>()
            .AddScoped<InfoCarrier.Core.Expressions.TypeNodeMapper>()
            .AddScoped<InfoCarrier.Core.Expressions.TypeNodeResolver>()
            .AddScoped<InfoCarrier.Core.Expressions.IDynamicValueMapper, InfoCarrier.Core.Expressions.DynamicValueMapper>()
            .AddScoped<InfoCarrier.Core.Expressions.ExpressionToNodeTranslator>()
            .AddDbContext<SmokeContext>(b => b.UseInMemoryDatabase(databaseName))
            .AddScoped<DbContext>(sp => sp.GetRequiredService<SmokeContext>())
            .BuildServiceProvider(validateScopes: true);

        using (IServiceScope scope = serverProvider.CreateScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<SmokeContext>();
            seed.Blogs.AddRange(new Blog { Id = 1, Title = "alpha" }, new Blog { Id = 2, Title = "beta" });
            await seed.SaveChangesAsync();
        }

        var envelopeServer = new InfoCarrierEnvelopeServer(
            new InProcessInfoCarrierServer(serverProvider), Serializer);
        var client = new TransportInfoCarrierClient(
            new InProcessInfoCarrierTransport(envelopeServer.DispatchAsync, Serializer), Serializer);

        return new SmokeContext(new DbContextOptionsBuilder<SmokeContext>().UseInfoCarrier(client).Options);
    }

    private sealed class ThrowingServer(Exception failure) : IInfoCarrierServer
    {
        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest r, CancellationToken c = default) => throw failure;

        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest r, CancellationToken c = default) => throw failure;

        public Task<TransactionResult> BeginTransactionAsync(CancellationToken c = default) => throw failure;

        public Task CommitTransactionAsync(string t, CancellationToken c = default) => throw failure;

        public Task RollbackTransactionAsync(string t, CancellationToken c = default) => throw failure;

        public Task CreateSavepointAsync(string t, string n, CancellationToken c = default) => throw failure;

        public Task RollbackToSavepointAsync(string t, string n, CancellationToken c = default) => throw failure;

        public Task ReleaseSavepointAsync(string t, string n, CancellationToken c = default) => throw failure;

        public Task<bool> SupportsSavepointsAsync(string t, CancellationToken c = default) => throw failure;
    }

    private sealed class TokenRecordingServer : IInfoCarrierServer
    {
        public List<CancellationToken> Tokens { get; } = [];

        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(new QueryDataResult { SerializedResults = [], IsEntityResult = false });
        }

        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(new SaveChangesResult { Count = 0, GeneratedValues = [] });
        }

        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(new TransactionResult { TransactionId = "t" });
        }

        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(true);
        }
    }
}
