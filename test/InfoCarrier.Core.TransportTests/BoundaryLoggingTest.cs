// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class BoundaryLoggingTest
{
    [Theory]
    [InlineData(false, LogLevel.Error)]
    [InlineData(true, LogLevel.Debug)]
    public async Task Injected_logger_reports_faults_without_copying_secrets(bool cancelled, LogLevel level)
    {
        var logs = new BoundaryLogs();
        using var services = new ServiceCollection().AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(logs)).BuildServiceProvider();
        using var token = new CancellationTokenSource();
        Exception failure = cancelled ? new OperationCanceledException("secret-message") : new InvalidOperationException("secret-message");
        failure.Data["secret-data"] = "secret-value";
        if (cancelled) { token.Cancel(); }
        var dispatcher = ActivatorUtilities.CreateInstance<InfoCarrierEnvelopeServer>(services,
            new EnvelopeFailureLoggingTest.TestServer(failure), new SystemTextJsonInfoCarrierSerializer());
        var request = new InfoCarrierEnvelope
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = InfoCarrierOperation.BeginTransaction,
            CorrelationId = "secret-correlation",
            Payload = [],
        };
        if (cancelled)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(request, token.Token)));
        }
        else
        {
            Assert.Equal(failure.Message, (await dispatcher.DispatchAsync(request)).Fault!.Message);
        }
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(typeof(InfoCarrierEnvelopeServer).FullName, entry.Category);
        Assert.Equal(level, entry.Level);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.Contains("BeginTransaction", entry.Message);
    }
}

internal sealed class BoundaryLogs : ILoggerProvider
{
    internal sealed record Entry(string Category, LogLevel Level, int EventId, string Message, Exception? Exception);
    public System.Collections.Concurrent.ConcurrentQueue<Entry> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Recorder(this, categoryName);
    public void Dispose() { }
    private sealed class Recorder(BoundaryLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (category.StartsWith("InfoCarrier.Core.", StringComparison.Ordinal) || category == "Microsoft.EntityFrameworkCore.Database.Command")
            {
                owner.Entries.Enqueue(new(category, level, id.Id, formatter(state, exception), exception));
            }
        }
    }
}
