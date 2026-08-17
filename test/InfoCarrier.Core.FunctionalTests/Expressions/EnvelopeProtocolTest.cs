// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using System.Text.Json;
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

    /// <summary>
    ///     An unknown operation comes back as a <em>fault</em>, not as a thrown exception. The two
    ///     ends still agree what an envelope is — they disagree about what is in it — so the
    ///     protocol can carry the refusal, and W5 says a failure the protocol can carry is one it
    ///     must carry.
    /// </summary>
    [Fact]
    public async Task An_unknown_operation_is_refused_by_name_as_a_fault()
    {
        InfoCarrierEnvelope response = await Server(new ThrowingServer()).DispatchAsync(
            Envelope((InfoCarrierOperation)99));

        Assert.NotNull(response.Fault);
        Assert.Equal(typeof(NotSupportedException).FullName, response.Fault!.TypeName);
        Assert.Contains("99", response.Fault.Message);

        // And rehydrates to the type the caller would have seen in-process.
        Assert.IsType<NotSupportedException>(InfoCarrierFaultMapper.Rehydrate(response.Fault));
    }

    /// <summary>
    ///     A version mismatch is the one failure that still escapes rather than travelling as a
    ///     fault: the two ends do not agree what an envelope <em>is</em>, so answering with one
    ///     assumes the thing in dispute.
    /// </summary>
    [Fact]
    public async Task A_version_mismatch_escapes_rather_than_becoming_a_fault()
        => await Assert.ThrowsAsync<NotSupportedException>(
            () => Server(new ThrowingServer()).DispatchAsync(
                Envelope(InfoCarrierOperation.BeginTransaction, version: 2)));

    /// <summary>
    ///     A server-side failure comes back with its type, message and inner chain intact — which
    ///     is what EF's spec tests assert on, and the whole point of W5.
    /// </summary>
    [Fact]
    public async Task A_server_failure_keeps_its_type_message_and_inner_chain()
    {
        InfoCarrierEnvelope response = await Server(
                new FailingServer(new InvalidOperationException("outer", new FormatException("inner"))))
            .DispatchAsync(Envelope(InfoCarrierOperation.BeginTransaction));

        Assert.NotNull(response.Fault);

        Exception rebuilt = InfoCarrierFaultMapper.Rehydrate(response.Fault!);

        Assert.IsType<InvalidOperationException>(rebuilt);
        Assert.Equal("outer", rebuilt.Message);
        Assert.IsType<FormatException>(rebuilt.InnerException);
        Assert.Equal("inner", rebuilt.InnerException!.Message);

        // The server's stack is kept where it cannot disturb a message assertion.
        Assert.NotNull(rebuilt.Data[InfoCarrierFaultMapper.ServerStackTraceKey]);
    }

    /// <summary>
    ///     An exception type this process cannot rebuild degrades to
    ///     <see cref="InfoCarrierServerException" /> and still names the original. A
    ///     store-specific exception is the real case: the client assembly has no reason to
    ///     reference the backend that threw it.
    /// </summary>
    [Fact]
    public void An_unresolvable_exception_type_degrades_but_keeps_the_name()
    {
        Exception rebuilt = InfoCarrierFaultMapper.Rehydrate(new InfoCarrierFault
        {
            TypeName = "Some.Backend.SpecificException",
            Message = "the store said no",
        });

        var server = Assert.IsType<InfoCarrierServerException>(rebuilt);
        Assert.Equal("Some.Backend.SpecificException", server.ServerExceptionTypeName);
        Assert.Equal("the store said no", server.Message);
    }

    /// <summary>
    ///     A failure whose runtime type is <c>internal</c> comes back as the nearest type a caller
    ///     can name (C83).
    /// </summary>
    /// <remarks>
    ///     `System.Text.Json` reports malformed JSON as `JsonReaderException`, which is internal
    ///     and derives from the public `JsonException`. Nobody can write
    ///     `catch (JsonReaderException)`, so degrading to `InfoCarrierServerException` threw away
    ///     the only name that was ever usable — which is what four `AdHocJsonQuery` tests
    ///     asserting `ThrowsAny<JsonException>` found.
    /// </remarks>
    [Fact]
    public void A_non_public_exception_type_degrades_to_the_nearest_public_base()
    {
        // Through `Utf8JsonReader` rather than `JsonSerializer`: the serializer catches and
        // rethrows the public `JsonException`, and it is the reader — which is what EF uses to
        // read a JSON column — that surfaces the internal one.
        Exception thrown = Assert.ThrowsAny<JsonException>(ReadInvalidJson);

        Assert.False(thrown.GetType().IsVisible, "the premise: the runtime type is not nameable");

        Exception rebuilt = InfoCarrierFaultMapper.Rehydrate(InfoCarrierFaultMapper.Capture(thrown));

        Assert.IsType<JsonException>(rebuilt);
        Assert.Equal(thrown.Message, rebuilt.Message);
    }

    private static void ReadInvalidJson()
    {
        var reader = new Utf8JsonReader("{ n:1 }"u8);
        while (reader.Read())
        {
        }
    }

    /// <summary>
    ///     And a <em>public</em> type with no usable constructor still degrades to
    ///     <see cref="InfoCarrierServerException" /> rather than to its base.
    /// </summary>
    /// <remarks>
    ///     The narrowing that makes the rule above safe. A public name is something the caller can
    ///     act on, and a base may mean something quite different: `SqliteException` derives from
    ///     `ExternalException`, which says nothing about a store.
    /// </remarks>
    [Fact]
    public void A_public_exception_type_with_no_usable_constructor_keeps_the_fallback()
    {
        Exception rebuilt = InfoCarrierFaultMapper.Rehydrate(new InfoCarrierFault
        {
            TypeName = typeof(PublicExceptionWithNoUsableConstructor).FullName!,
            Message = "the store said no",
        });

        var server = Assert.IsType<InfoCarrierServerException>(rebuilt);
        Assert.Equal(typeof(PublicExceptionWithNoUsableConstructor).FullName, server.ServerExceptionTypeName);
    }

    /// <summary>
    ///     Public, derives from a constructible public base, and offers no constructor this mapper
    ///     will use — the shape `SqliteException` has.
    /// </summary>
    public class PublicExceptionWithNoUsableConstructor(string message, int code)
        : InvalidOperationException(message)
    {
        public int Code { get; } = code;
    }

    /// <summary>
    ///     Rehydration never loads an assembly and never constructs a non-exception, whatever the
    ///     payload names.
    /// </summary>
    [Theory]
    [InlineData("System.Text.StringBuilder")]
    [InlineData("System.Diagnostics.Process")]
    [InlineData("System.Exception+NotAThing")]
    public void Rehydration_refuses_a_type_that_is_not_an_exception(string typeName)
        => Assert.IsType<InfoCarrierServerException>(
            InfoCarrierFaultMapper.Rehydrate(
                new InfoCarrierFault { TypeName = typeName, Message = "x" }));

    private sealed class FailingServer(Exception failure) : IInfoCarrierServer
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

    /// <summary>
    ///     Every operation the enum declares dispatches to the matching server method. This is
    ///     the test the smoke test's old inline dispatcher could not be: it handled
    ///     <c>Query</c> and threw for the other eight.
    /// </summary>
    /// <remarks>
    ///     <b><c>Query</c> goes through its own entry point since D7 half (A)</b>, because its
    ///     answer is a stream rather than an envelope — so it is dispatched here through
    ///     <see cref="InfoCarrierEnvelopeServer.DispatchQueryAsync" />, and the count assertion at
    ///     the end still holds every operation to having been exercised. Draining the sequence is
    ///     what makes the call happen at all: an async iterator's body does not run until it is
    ///     enumerated, so asserting on <c>Calls</c> without the <c>await foreach</c> would pass
    ///     against a server that was never asked anything.
    /// </remarks>
    [Fact]
    public async Task Every_declared_operation_dispatches_to_its_own_server_method()
    {
        var server = new RecordingServer();
        InfoCarrierEnvelopeServer envelopeServer = Server(server);

        await foreach (QueryStreamItem _ in envelopeServer.DispatchQueryAsync(Envelope(
            InfoCarrierOperation.Query, new QueryDataRequest
            {
                SerializedQuery = Serializer.Serialize(1),
                TrackingBehavior = Microsoft.EntityFrameworkCore.QueryTrackingBehavior.TrackAll,
                IsAsync = false,
                ReturnsSingleResult = false,
            })))
        {
            // Drained for its effect; the items themselves are `DispatchQueryAsync`'s own tests.
        }
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
            return Task.FromResult(new QueryDataResult { Rows = System.Linq.AsyncEnumerable.Empty<InfoCarrier.Core.Expressions.DynamicValueNode>(), IsEntityResult = false });
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
