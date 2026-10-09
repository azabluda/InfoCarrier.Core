// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class EnvelopeFailureLoggingTest
{
    private static readonly SystemTextJsonInfoCarrierSerializer Serializer = new();
    private static InfoCarrierEnvelope Request => new()
    {
        ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
        Operation = InfoCarrierOperation.BeginTransaction,
        CorrelationId = "secret-correlation\n",
        Payload = Serializer.Serialize<object?>(null),
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Serialization_failure_distinguishes_completed_operation_from_fault_response(bool faultPlaceholder)
    {
        var logs = new FailureLogs();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var server = new TestServer(faultPlaceholder ? new InvalidOperationException("secret-original") : null);
        var dispatcher = new InfoCarrierEnvelopeServer(server, new FailingSerializer(faultPlaceholder), factory.CreateLogger<InfoCarrierEnvelopeServer>());
        if (faultPlaceholder) { await Assert.ThrowsAsync<FormatException>(() => dispatcher.DispatchAsync(Request)); }
        else
        {
            Assert.NotNull((await dispatcher.DispatchAsync(Request)).Fault);
            Assert.Equal(1, server.Completed);
        }
        var entry = Assert.Single(logs.Entries);
        Assert.Contains(faultPlaceholder ? "fault response" : "after operation completion", entry.Message);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("secret", entry.Message);
    }

    [Fact]
    public async Task Concurrent_dispatches_have_independent_outcomes_without_shared_suppression()
    {
        var logs = new FailureLogs();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals = 0;
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(new InvalidOperationException("secret"))
        {
            BeforeBegin = async () =>
            {
                if (Interlocked.Increment(ref arrivals) == 20) { entered.TrySetResult(); }
                await release.Task;
            },
        }, Serializer, factory.CreateLogger<InfoCarrierEnvelopeServer>());
        Task<InfoCarrierEnvelope>[] requests = Enumerable.Range(0, 20).Select(_ => dispatcher.DispatchAsync(Request)).ToArray();
        try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { release.TrySetResult(); }
        var responses = await Task.WhenAll(requests);
        Assert.All(responses, response => Assert.NotNull(response.Fault));
        Assert.Equal(20, logs.Entries.Count);
        Assert.All(logs.Entries, entry => Assert.DoesNotContain("secret", entry.Message));
    }

    [Fact]
    public async Task Logger_failure_never_replaces_the_original_fault()
    {
        using var factory = LoggerFactory.Create(b => b.AddProvider(new ThrowingLogs()));
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(new InvalidOperationException("original")), Serializer,
            factory.CreateLogger<InfoCarrierEnvelopeServer>());
        Assert.Equal("original", (await dispatcher.DispatchAsync(Request)).Fault!.Message);
    }

    [Fact]
    public async Task Concurrency_conflict_keeps_its_fault_and_uses_information()
    {
        var logs = new FailureLogs();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(new DbUpdateConcurrencyException("secret")), Serializer,
            factory.CreateLogger<InfoCarrierEnvelopeServer>());
        Assert.Equal("secret", (await dispatcher.DispatchAsync(Request)).Fault!.Message);
        Assert.Equal(LogLevel.Information, Assert.Single(logs.Entries).Level);
    }

    [Fact]
    public async Task Nested_dispatches_log_each_failed_boundary()
    {
        var logs = new FailureLogs();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var logger = factory.CreateLogger<InfoCarrierEnvelopeServer>();
        var inner = new InfoCarrierEnvelopeServer(new TestServer(new InvalidOperationException("secret-inner")), Serializer, logger);
        var outer = new InfoCarrierEnvelopeServer(new TestServer(new InvalidOperationException("secret-outer"))
        {
            BeforeBegin = async () => Assert.NotNull((await inner.DispatchAsync(Request)).Fault),
        }, Serializer, logger);
        Assert.NotNull((await outer.DispatchAsync(Request)).Fault);
        Assert.Equal(2, logs.Entries.Count);
        Assert.All(logs.Entries, entry => Assert.DoesNotContain("secret", entry.Message));
    }

    [Fact]
    public async Task Unrelated_cancellation_remains_an_error_and_propagates()
    {
        var logs = new FailureLogs();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var failure = new OperationCanceledException("secret");
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(failure), Serializer, factory.CreateLogger<InfoCarrierEnvelopeServer>());
        Assert.Same(failure, await Assert.ThrowsAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(Request)));
        Assert.Equal(LogLevel.Error, Assert.Single(logs.Entries).Level);
    }

    internal sealed class TestServer(Exception? failure) : IInfoCarrierServer
    {
        public int Completed;
        public Func<Task>? BeforeBegin { get; init; }
        public async Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (BeforeBegin is { } before) { await before(); }
            if (failure is not null) { throw failure; }
            Interlocked.Increment(ref Completed);
            return new TransactionResult { TransactionId = "secret-transaction" };
        }
        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default) => throw failure ?? new NotSupportedException();
        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default) => throw failure ?? new NotSupportedException();
        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default) => throw failure ?? new NotSupportedException();
        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default) => throw failure ?? new NotSupportedException();
        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => throw failure ?? new NotSupportedException();
        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => throw failure ?? new NotSupportedException();
        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => throw failure ?? new NotSupportedException();
        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default) => throw failure ?? new NotSupportedException();
    }

    private sealed class FailingSerializer(bool faultPlaceholder) : IInfoCarrierSerializer
    {
        public byte[] Serialize<T>(T value)
        {
            if (faultPlaceholder || value is TransactionResult) { throw new FormatException("secret-serialization"); }
            return Serializer.Serialize(value);
        }
        public T? Deserialize<T>(byte[] payload) => Serializer.Deserialize<T>(payload);
        public ValueTask<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default) => ValueTask.FromResult(Serialize(value));
        public ValueTask<T?> DeserializeAsync<T>(byte[] payload, CancellationToken cancellationToken = default) => ValueTask.FromResult(Deserialize<T>(payload));
    }

    private sealed class ThrowingLogs : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ThrowingLogger();
        public void Dispose() { }
        private sealed class ThrowingLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => throw new InvalidOperationException("logger failure");
        }
    }
}
