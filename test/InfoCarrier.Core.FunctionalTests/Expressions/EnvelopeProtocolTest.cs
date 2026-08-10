// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Expressions;

/// <summary>
///     The envelope protocol's server half (wire-protocol §1, milestone M5): version checking
///     and operation dispatch.
/// </summary>
/// <remarks>
///     The whole functional suite now runs through <see cref="InfoCarrierEnvelopeServer" />, so
///     the happy path of every operation is covered many thousands of times over. What that
///     cannot cover is the refusals — a suite that only ever sends the current version never
///     finds out what happens to a different one.
/// </remarks>
public class EnvelopeProtocolTest
{
    private static readonly SystemTextJsonInfoCarrierSerializer Serializer = new();

    private static InfoCarrierEnvelopeServer Server(IInfoCarrierServer server)
        => new(server, Serializer);

    private static InfoCarrierEnvelope Envelope(
        InfoCarrierOperation operation, object? payload = null, int? version = null)
        => new()
        {
            ProtocolVersion = version ?? InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = operation,
            Payload = Serializer.Serialize(payload),
        };

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task An_unsupported_protocol_version_is_refused_by_number(int version)
    {
        NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => Server(new ThrowingServer()).DispatchAsync(
                Envelope(InfoCarrierOperation.BeginTransaction, version: version)));

        // Both numbers: a client one release ahead has to learn which version it is talking to.
        Assert.Contains(version.ToString(), ex.Message);
        Assert.Contains(InfoCarrierEnvelope.CurrentProtocolVersion.ToString(), ex.Message);
    }

    /// <summary>
    ///     The version is checked <em>before</em> the operation runs, not after. A server that
    ///     executed first and complained afterwards would have already done the work.
    /// </summary>
    [Fact]
    public async Task The_version_is_checked_before_the_operation_runs()
    {
        var server = new RecordingServer();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => Server(server).DispatchAsync(
                Envelope(InfoCarrierOperation.BeginTransaction, version: 7)));

        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task An_unknown_operation_is_refused_by_name()
    {
        NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => Server(new ThrowingServer()).DispatchAsync(
                Envelope((InfoCarrierOperation)99)));

        Assert.Contains("99", ex.Message);
    }

    /// <summary>
    ///     Every operation the enum declares dispatches to the matching server method. This is
    ///     the test the smoke test's old inline dispatcher could not be: it handled
    ///     <c>Query</c> and threw for the other eight.
    /// </summary>
    [Fact]
    public async Task Every_declared_operation_dispatches_to_its_own_server_method()
    {
        var server = new RecordingServer();
        InfoCarrierEnvelopeServer envelopeServer = Server(server);

        await envelopeServer.DispatchAsync(Envelope(
            InfoCarrierOperation.Query, new QueryDataRequest
            {
                SerializedQuery = Serializer.Serialize(1),
                TrackingBehavior = Microsoft.EntityFrameworkCore.QueryTrackingBehavior.TrackAll,
                IsAsync = false,
                ReturnsSingleResult = false,
            }));
        await envelopeServer.DispatchAsync(Envelope(
            InfoCarrierOperation.SaveChanges, new SaveChangesRequest { Entries = [] }));
        await envelopeServer.DispatchAsync(Envelope(InfoCarrierOperation.BeginTransaction));
        await envelopeServer.DispatchAsync(Envelope(InfoCarrierOperation.CommitTransaction, "t1"));
        await envelopeServer.DispatchAsync(Envelope(InfoCarrierOperation.RollbackTransaction, "t2"));
        await envelopeServer.DispatchAsync(Envelope(
            InfoCarrierOperation.CreateSavepoint, new SavepointRequest { TransactionId = "t3", Name = "s1" }));
        await envelopeServer.DispatchAsync(Envelope(
            InfoCarrierOperation.RollbackToSavepoint, new SavepointRequest { TransactionId = "t4", Name = "s2" }));
        await envelopeServer.DispatchAsync(Envelope(
            InfoCarrierOperation.ReleaseSavepoint, new SavepointRequest { TransactionId = "t5", Name = "s3" }));
        await envelopeServer.DispatchAsync(Envelope(InfoCarrierOperation.SupportsSavepoints, "t6"));

        Assert.Equal(
            [
                "Query", "SaveChanges", "BeginTransaction",
                "CommitTransaction:t1", "RollbackTransaction:t2",
                "CreateSavepoint:t3/s1", "RollbackToSavepoint:t4/s2", "ReleaseSavepoint:t5/s3",
                "SupportsSavepoints:t6",
            ],
            server.Calls);

        // Every operation the enum declares was exercised above — so a new member added without
        // a dispatch arm fails here rather than at a caller's first use of it.
        Assert.Equal(Enum.GetValues<InfoCarrierOperation>().Length, server.Calls.Count);
    }

    /// <summary>
    ///     The response keeps the request's version and operation, so a caller correlating them
    ///     is not left guessing which request an answer belongs to.
    /// </summary>
    [Fact]
    public async Task The_response_echoes_the_version_operation_and_correlation_id()
    {
        InfoCarrierEnvelope request = Envelope(InfoCarrierOperation.BeginTransaction) with
        {
            CorrelationId = "abc",
        };

        InfoCarrierEnvelope response = await Server(new RecordingServer()).DispatchAsync(request);

        Assert.Equal(request.ProtocolVersion, response.ProtocolVersion);
        Assert.Equal(request.Operation, response.Operation);
        Assert.Equal("abc", response.CorrelationId);
        Assert.NotNull(Serializer.Deserialize<TransactionResult>(response.Payload));
    }

    /// <summary>
    ///     An operation that returns nothing still writes a payload — the client deserializes one
    ///     either way, and a zero-length body is not valid JSON.
    /// </summary>
    [Fact]
    public async Task A_void_operation_still_returns_a_readable_payload()
    {
        InfoCarrierEnvelope response = await Server(new RecordingServer()).DispatchAsync(
            Envelope(InfoCarrierOperation.CommitTransaction, "t"));

        Assert.NotEmpty(response.Payload);
        Assert.Null(Serializer.Deserialize<object>(response.Payload));
    }

    private sealed class RecordingServer : IInfoCarrierServer
    {
        public List<string> Calls { get; } = [];

        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add("Query");
            return Task.FromResult(new QueryDataResult { SerializedResults = [], IsEntityResult = false });
        }

        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add("SaveChanges");
            return Task.FromResult(new SaveChangesResult { Count = 0, GeneratedValues = [] });
        }

        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("BeginTransaction");
            return Task.FromResult(new TransactionResult { TransactionId = "new" });
        }

        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"CommitTransaction:{transactionId}");
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"RollbackTransaction:{transactionId}");
            return Task.CompletedTask;
        }

        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        {
            Calls.Add($"CreateSavepoint:{transactionId}/{name}");
            return Task.CompletedTask;
        }

        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        {
            Calls.Add($"RollbackToSavepoint:{transactionId}/{name}");
            return Task.CompletedTask;
        }

        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        {
            Calls.Add($"ReleaseSavepoint:{transactionId}/{name}");
            return Task.CompletedTask;
        }

        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"SupportsSavepoints:{transactionId}");
            return Task.FromResult(true);
        }
    }

    /// <summary>A server that must never be reached.</summary>
    private sealed class ThrowingServer : IInfoCarrierServer
    {
        private static Task<T> No<T>() => throw new InvalidOperationException("The server was reached.");

        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest r, CancellationToken c = default) => No<QueryDataResult>();

        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest r, CancellationToken c = default) => No<SaveChangesResult>();

        public Task<TransactionResult> BeginTransactionAsync(CancellationToken c = default) => No<TransactionResult>();

        public Task CommitTransactionAsync(string t, CancellationToken c = default) => No<object>();

        public Task RollbackTransactionAsync(string t, CancellationToken c = default) => No<object>();

        public Task CreateSavepointAsync(string t, string n, CancellationToken c = default) => No<object>();

        public Task RollbackToSavepointAsync(string t, string n, CancellationToken c = default) => No<object>();

        public Task ReleaseSavepointAsync(string t, string n, CancellationToken c = default) => No<object>();

        public Task<bool> SupportsSavepointsAsync(string t, CancellationToken c = default) => No<bool>();
    }
}
