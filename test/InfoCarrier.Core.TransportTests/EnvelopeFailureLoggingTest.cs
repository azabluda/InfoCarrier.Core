// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.Metrics;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
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
    [InlineData("unexpected", "ExecutionFailure", LogLevel.Error)]
    [InlineData("database", "Database", LogLevel.Error)]
    [InlineData("concurrency", "Concurrency", LogLevel.Information)]
    public async Task Non_http_faults_preserve_the_exception_contract_and_log_only_safe_fields(string kind, string reason, LogLevel level)
    {
        Exception failure = kind switch
        {
            "database" => new DbUpdateException("secret-message", new FormatException("secret-inner")),
            "concurrency" => new DbUpdateConcurrencyException("secret-message"),
            _ => new InvalidOperationException("secret-message", new FormatException("secret-inner")),
        };
        failure.Data["secret-data"] = "secret-value";
        var logs = new FailureLogs();
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(failure), Serializer, Diagnostics(provider), factory);
        InfoCarrierEnvelope response = await dispatcher.DispatchAsync(Request);
        Assert.Equal(failure.Message, response.Fault!.Message);
        Assert.Equal(failure.GetType().FullName, response.Fault.TypeName);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(reason, entry.Properties["Reason"]);
        Assert.Equal(level, entry.Level);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.All(entry.Properties.Values, v => Assert.DoesNotContain("secret", v?.ToString() ?? ""));
    }

    [Theory]
    [InlineData(true, "Cancellation", LogLevel.Information)]
    [InlineData(false, "UnexpectedCancellation", LogLevel.Error)]
    public async Task Cancellation_propagates_and_uses_the_request_token_for_severity(bool cancelled, string reason, LogLevel level)
    {
        using var token = new CancellationTokenSource();
        if (cancelled) { token.Cancel(); }
        var failure = new OperationCanceledException("secret-cancellation");
        var logs = new FailureLogs();
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(failure), Serializer, Diagnostics(provider), factory);
        Assert.Same(failure, await Assert.ThrowsAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(Request, token.Token)));
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(reason, entry.Properties["Reason"]);
        Assert.Equal(level, entry.Level);
    }

    [Theory]
    [InlineData(false, "ResultSerialization")]
    [InlineData(true, "FaultSerialization")]
    public async Task Serialization_failures_name_the_phase_after_execution(bool faultPlaceholder, string phase)
    {
        var logs = new FailureLogs();
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var server = new TestServer(faultPlaceholder ? new InvalidOperationException("secret-original") : null);
        var serializer = new FailingSerializer(faultPlaceholder);
        var dispatcher = new InfoCarrierEnvelopeServer(server, serializer, Diagnostics(provider), factory);
        if (faultPlaceholder)
        {
            await Assert.ThrowsAsync<FormatException>(() => dispatcher.DispatchAsync(Request));
        }
        else
        {
            Assert.NotNull((await dispatcher.DispatchAsync(Request)).Fault);
            Assert.Equal(1, server.Completed);
        }
        var entry = Assert.Single(logs.Entries);
        Assert.Equal("ResponseSerialization", entry.Properties["Reason"]);
        Assert.Equal(phase, entry.Properties["Phase"]);
    }

    [Fact]
    public async Task Repeated_failures_are_bounded_and_counters_include_suppressed_events()
    {
        var clock = new ManualClock();
        var logs = new FailureLogs();
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(new DbUpdateConcurrencyException("secret")), Serializer,
            Diagnostics(provider, clock), factory);
        long failures = 0, suppressed = 0;
        var observing = new AsyncLocal<bool>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) => { if (instrument.Meter.Name == "InfoCarrier.Server") { l.EnableMeasurementEvents(instrument); } };
        listener.SetMeasurementEventCallback<long>((instrument, count, tags, state) =>
        {
            if (!observing.Value) { return; }
            Assert.All(tags.ToArray(), tag => Assert.Equal("reason", tag.Key));
            if (instrument.Name == "infocarrier.server.failures") { Interlocked.Add(ref failures, count); }
            if (instrument.Name == "infocarrier.server.suppressed") { Interlocked.Add(ref suppressed, count); }
        });
        listener.Start();
        observing.Value = true;
        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => dispatcher.DispatchAsync(Request)));
        Assert.Equal(5, logs.Entries.Count);
        Assert.Equal(100, failures);
        Assert.Equal(95, suppressed);
        clock.Advance();
        await dispatcher.DispatchAsync(Request);
        Assert.Equal(7, logs.Entries.Count);
        Assert.Equal(95L, logs.Entries.Single(e => e.Id.Name == "FailuresSuppressed").Properties["SuppressedCount"]);
        Assert.Equal(101, failures);
        observing.Value = false;
    }

    [Fact]
    public async Task A_null_inner_payload_is_an_input_refusal()
    {
        var logs = new FailureLogs();
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(null), Serializer, Diagnostics(provider), factory);
        var response = await dispatcher.DispatchAsync(Request with { Operation = InfoCarrierOperation.Query, Payload = null! });
        Assert.Equal(typeof(NullReferenceException).FullName, response.Fault!.TypeName);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal("InvalidPayload", entry.Properties["Reason"]);
        Assert.Equal("Input", entry.Properties["Phase"]);
    }

    [Fact]
    public async Task Logger_failures_never_replace_faults()
    {
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(new ThrowingLogs()));
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(new InvalidOperationException("original")), Serializer,
            Diagnostics(provider), factory);
        Assert.Equal("original", (await dispatcher.DispatchAsync(Request)).Fault!.Message);
    }


    [Fact]
    public async Task Overlapping_requests_keep_separate_outcomes_and_identifiers()
    {
        var logs = new FailureLogs();
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var factory = LoggerFactory.Create(b => b.AddProvider(logs));
        var diagnostics = Diagnostics(provider);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals = 0;
        async Task WaitTogether()
        {
            if (Interlocked.Increment(ref arrivals) == 3) { entered.TrySetResult(); }
            await release.Task;
        }
        Exception[] errors = [new InvalidOperationException("secret"), new DbUpdateException("secret"), new DbUpdateConcurrencyException("secret")];
        Task<InfoCarrierEnvelope>[] requests = errors.Select(error => new InfoCarrierEnvelopeServer(
            new TestServer(error) { BeforeBegin = WaitTogether }, Serializer, diagnostics, factory).DispatchAsync(Request)).ToArray();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Empty(logs.Entries);
        }
        finally { release.TrySetResult(); }
        await Task.WhenAll(requests);
        Assert.Equal(3, logs.Entries.Count);
        Assert.Equal(3, logs.Entries.Select(e => e.Properties["RequestId"]).Distinct().Count());
        Assert.Equal(new[] { "Concurrency", "Database", "ExecutionFailure" },
            logs.Entries.Select(e => e.Properties["Reason"]!.ToString()).Order());
    }

    [Fact]
    public async Task A_failing_metric_listener_cannot_replace_a_fault()
    {
        using var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "InfoCarrier.Server") { l.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw new InvalidOperationException("secret-listener"));
        listener.Start();
        var dispatcher = new InfoCarrierEnvelopeServer(new TestServer(new InvalidOperationException("original")), Serializer, Diagnostics(provider));
        Assert.Equal("original", (await dispatcher.DispatchAsync(Request)).Fault!.Message);
    }

    private static InfoCarrierServerDiagnostics Diagnostics(ServiceProvider provider, TimeProvider? clock = null)
        => new(provider.GetRequiredService<IMeterFactory>(), clock ?? TimeProvider.System);

    internal sealed class ManualClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Volatile.Read(ref _ticks);
        public void Advance() => Interlocked.Add(ref _ticks, TimeSpan.FromMinutes(1).Ticks);
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
