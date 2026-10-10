// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class ClientFailureLoggingTest
{
    [Theory]
    [InlineData("request payload serialization", false, false, LogLevel.Error)]
    [InlineData("transport exchange", false, false, LogLevel.Error)]
    [InlineData("response payload deserialization", false, false, LogLevel.Error)]
    [InlineData("returned server fault", false, false, LogLevel.Information)]
    [InlineData("transport exchange", true, true, LogLevel.Debug)]
    [InlineData("transport exchange", true, false, LogLevel.Error)]
    public async Task Failure_reports_local_phase_without_exception_contents(
        string phase, bool cancelled, bool cancelToken, LogLevel level)
    {
        var logs = new Capture();
        using var cancellation = new CancellationTokenSource();
        if (cancelToken) { cancellation.Cancel(); }
        Exception failure = cancelled ? new OperationCanceledException("secret-message") : new InvalidOperationException("secret-message");
        failure.Data["secret-data"] = "secret-value";
        var serializer = new FailingSerializer(phase, failure);
        var client = Create(new Transport(phase, failure), serializer, logs);

        Exception? thrown = await Record.ExceptionAsync(() => client.CommitTransactionAsync("secret-token", cancellation.Token));

        Assert.NotNull(thrown);
        if (phase != "returned server fault") { Assert.Same(failure, thrown); }
        Entry entry = Assert.Single(logs.Entries);
        Assert.Equal(35100, entry.Id);
        Assert.Equal(level, entry.Level);
        Assert.Contains(phase, entry.Message);
        Assert.Contains("CommitTransaction", entry.Message);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.Equal(phase, entry.State["Phase"]);
        Assert.Equal(thrown.GetType().FullName, entry.State["ExceptionType"]);
        Assert.Equal(cancelled ? (cancelToken ? "CallerCancellation" : "UnrelatedCancellation")
            : phase == "returned server fault" ? "ServerFaultReceived" : "UnclassifiedFailure", entry.State["Outcome"]);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task Successful_exchange_is_silent()
    {
        var logs = new Capture();
        var client = Create(new Transport("success", new Exception()), new SystemTextJsonInfoCarrierSerializer(), logs);
        await client.CommitTransactionAsync("secret-token");
        Assert.Empty(logs.Entries);
    }

    [Fact]
    public async Task Delivery_failure_does_not_replace_the_operation_failure()
    {
        var failure = new InvalidOperationException("original");
        var client = Create(new Transport("transport exchange", failure), new SystemTextJsonInfoCarrierSerializer(), new Capture { Throw = true });
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => client.CommitTransactionAsync("t")));
    }

    [Theory]
    [InlineData(InfoCarrierOperation.Query)]
    [InlineData(InfoCarrierOperation.SaveChanges)]
    [InlineData(InfoCarrierOperation.BeginTransaction)]
    [InlineData(InfoCarrierOperation.CommitTransaction)]
    [InlineData(InfoCarrierOperation.RollbackTransaction)]
    [InlineData(InfoCarrierOperation.CreateSavepoint)]
    [InlineData(InfoCarrierOperation.RollbackToSavepoint)]
    [InlineData(InfoCarrierOperation.ReleaseSavepoint)]
    [InlineData(InfoCarrierOperation.SupportsSavepoints)]
    public async Task Each_operation_reports_its_local_name(InfoCarrierOperation operation)
    {
        var logs = new Capture();
        var failure = new TimeoutException("secret", new Exception("secret-inner"));
        var client = Create(new Transport("transport exchange", failure), new SystemTextJsonInfoCarrierSerializer(), logs);
        using var context = new DbContext(new DbContextOptions<DbContext>());
        Task Invoke() => operation switch
        {
            InfoCarrierOperation.Query => client.QueryDataAsync(new QueryDataRequest
                { SerializedQuery = [1, 2, 3], TrackingBehavior = QueryTrackingBehavior.NoTracking, IsAsync = true, ReturnsSingleResult = false }, context),
            InfoCarrierOperation.SaveChanges => client.SaveChangesAsync(new SaveChangesRequest { Entries = [] }, context),
            InfoCarrierOperation.BeginTransaction => client.BeginTransactionAsync(),
            InfoCarrierOperation.CommitTransaction => client.CommitTransactionAsync("secret-token"),
            InfoCarrierOperation.RollbackTransaction => client.RollbackTransactionAsync("secret-token"),
            InfoCarrierOperation.CreateSavepoint => client.CreateSavepointAsync("secret-token", "secret-name"),
            InfoCarrierOperation.RollbackToSavepoint => client.RollbackToSavepointAsync("secret-token", "secret-name"),
            InfoCarrierOperation.ReleaseSavepoint => client.ReleaseSavepointAsync("secret-token", "secret-name"),
            InfoCarrierOperation.SupportsSavepoints => client.SupportsSavepointsAsync("secret-token"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        Assert.Same(failure, await Record.ExceptionAsync(Invoke));
        Entry entry = Assert.Single(logs.Entries);
        Assert.Equal(operation, entry.State["Operation"]);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.All(entry.State.Values, value => Assert.DoesNotContain("secret", value?.ToString() ?? ""));
    }

    [Fact]
    public async Task Concurrency_fault_is_informational_and_preserves_fault_type()
    {
        var logs = new Capture();
        var client = Create(new Transport("returned server fault", new DbUpdateConcurrencyException("secret")),
            new SystemTextJsonInfoCarrierSerializer(), logs);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => client.CommitTransactionAsync("t"));
        Entry entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("ConcurrencyConflict", entry.State["Outcome"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disabled_or_broken_enablement_preserves_failure(bool broken)
    {
        var logs = new Capture { Enabled = false, ThrowOnEnable = broken };
        var failure = new TimeoutException("original");
        var client = Create(new Transport("transport exchange", failure), new SystemTextJsonInfoCarrierSerializer(), logs);
        Assert.Same(failure, await Record.ExceptionAsync(() => client.CommitTransactionAsync("t")));
        Assert.Empty(logs.Entries);
    }

    [Fact]
    public void Explicit_null_logger_is_configuration_error()
        => Assert.Throws<ArgumentNullException>(() => new TransportInfoCarrierClient(
            new Transport("success", new Exception()), new SystemTextJsonInfoCarrierSerializer(), null!));

    private static TransportInfoCarrierClient Create(IInfoCarrierTransport transport, IInfoCarrierSerializer serializer, Capture logs)
        => new(transport, serializer, logs);

    private sealed record Entry(int Id, LogLevel Level, string Message, Exception? Exception, Dictionary<string, object?> State);

    private sealed class Capture : ILogger<TransportInfoCarrierClient>
    {
        public List<Entry> Entries { get; } = [];
        public bool Throw { get; init; }
        public bool Enabled { get; init; } = true;
        public bool ThrowOnEnable { get; init; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => ThrowOnEnable ? throw new InvalidOperationException("enablement") : Enabled;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (Throw) { throw new InvalidOperationException("logger backend"); }
            Entries.Add(new(id.Id, level, formatter(state, exception), exception,
                ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(x => x.Key, x => x.Value)));
        }
    }

    private sealed class Transport(string phase, Exception failure) : IInfoCarrierTransport
    {
        public Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => phase == "transport exchange" ? Task.FromException<InfoCarrierEnvelope>(failure) : Task.FromResult(request with
            {
                Payload = new SystemTextJsonInfoCarrierSerializer().Serialize<object?>(null),
                Fault = phase == "returned server fault" ? InfoCarrierFaultMapper.Capture(failure) : null,
            });
    }

    private sealed class FailingSerializer(string phase, Exception failure) : IInfoCarrierSerializer
    {
        private readonly SystemTextJsonInfoCarrierSerializer _inner = new();
        public byte[] Serialize<T>(T value) => _inner.Serialize(value);
        public T? Deserialize<T>(byte[] payload) => _inner.Deserialize<T>(payload);
        public ValueTask<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
            => phase == "request payload serialization" ? ValueTask.FromException<byte[]>(failure) : new(Serialize(value));
        public ValueTask<T?> DeserializeAsync<T>(byte[] payload, CancellationToken cancellationToken = default)
            => phase == "response payload deserialization" ? ValueTask.FromException<T?>(failure) : new(Deserialize<T>(payload));
    }
}
