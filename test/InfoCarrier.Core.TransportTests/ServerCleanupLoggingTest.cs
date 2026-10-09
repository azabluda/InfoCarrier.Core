// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class ServerCleanupLoggingTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_observes_all_failures_and_continues_with_other_transactions(bool idle)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int outcomes = 0;
        var logs = new FailureLogs(e =>
        {
            if (Equals(e.Properties.GetValueOrDefault("Reason"), "CleanupScopeDisposalFailure")
                && Interlocked.Increment(ref outcomes) == 2) { completed.TrySetResult(); }
        });
        var clock = new FakeTimeProvider();
        var interceptor = new FailingTransactions();
        int scopesDisposed = 0;
        await using var host = factory.WithWebHostBuilder(b => b
            .ConfigureLogging(l => l.AddProvider(logs))
            .ConfigureServices(s =>
            {
                s.AddSingleton<IInterceptor>(interceptor);
                s.AddSingleton<TimeProvider>(clock);
                if (idle) { s.AddInfoCarrierServerTransactionTimeout(TimeSpan.FromMinutes(10)); }
                s.AddScoped(_ => new FailingScope(() => Interlocked.Increment(ref scopesDisposed)));
                s.AddScoped<DbContext>(sp =>
                {
                    _ = sp.GetRequiredService<FailingScope>();
                    return sp.GetRequiredService<Northwind.Shared.NorthwindContext>();
                });
            }));
        _ = host.CreateClient();
        var server = (InProcessInfoCarrierServer)host.Services.GetRequiredService<IInfoCarrierServer>();
        await server.BeginTransactionAsync();
        await server.BeginTransactionAsync();
        if (idle)
        {
            clock.Advance(TimeSpan.FromMinutes(11));
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        else { await server.DisposeAsync(); }
        Assert.Equal(2, interceptor.Rollbacks);
        Assert.Equal(2, interceptor.Disposals);
        Assert.Equal(2, scopesDisposed);
        Assert.Equal(2, logs.Entries.Count(e => Equals(e.Properties.GetValueOrDefault("Reason"), "CleanupRollbackFailure")));
        Assert.Equal(2, logs.Entries.Count(e => Equals(e.Properties.GetValueOrDefault("Reason"), "CleanupTransactionDisposalFailure")));
        Assert.Equal(2, logs.Entries.Count(e => Equals(e.Properties.GetValueOrDefault("Reason"), "CleanupScopeDisposalFailure")));
        Assert.All(logs.Entries, e =>
        {
            Assert.Null(e.Exception);
            Assert.DoesNotContain("secret", e.Message);
        });
    }


    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Scope_disposal_outcomes_use_a_live_scoped_logger(bool disposeContainer, bool scopedFactory)
    {
        var logs = new FailureLogs();
        var interceptor = new FailingTransactions();
        var services = new ServiceCollection().AddMetrics().AddInfoCarrierServerDiagnostics();
        if (scopedFactory) { services.AddScoped<ILoggerFactory>(_ => new OwnedLoggerFactory(logs)); }
        else { services.AddSingleton<ILoggerFactory>(_ => new OwnedLoggerFactory(logs)); }
        services.AddDbContext<Northwind.Shared.NorthwindContext>(b => b
            .UseSqlite("Data Source=:memory:").AddInterceptors(interceptor));
        services.AddScoped(_ => new FailingScope(() => { }));
        services.AddScoped<DbContext>(sp =>
        {
            _ = sp.GetRequiredService<FailingScope>();
            return sp.GetRequiredService<Northwind.Shared.NorthwindContext>();
        });
        services.AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var server = (InProcessInfoCarrierServer)provider.GetRequiredService<IInfoCarrierServer>();
        await server.BeginTransactionAsync();
        if (disposeContainer) { await provider.DisposeAsync(); }
        else { await server.DisposeAsync(); }
        Assert.Single(logs.Entries, e => Equals(e.Properties.GetValueOrDefault("Reason"), "CleanupScopeDisposalFailure"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Normal_idle_eviction_is_truthful_and_preserves_existing_host_warnings(bool diagnosticsEnabled)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logs = new FailureLogs(e =>
        {
            if (Equals(e.Properties.GetValueOrDefault("Reason"), "CleanupCompleted")) { completed.TrySetResult(); }
        });
        var clock = new FakeTimeProvider();
        await using var host = factory.WithWebHostBuilder(b => b
            .ConfigureLogging(l => l.AddProvider(logs))
            .ConfigureServices(s =>
            {
                s.AddSingleton<TimeProvider>(clock);
                s.AddInfoCarrierServerTransactionTimeout(TimeSpan.FromMinutes(10));
                if (!diagnosticsEnabled) { s.RemoveAll<InfoCarrierServerDiagnostics>(); }
            }));
        using var client = host.CreateClient();
        var server = host.Services.GetRequiredService<IInfoCarrierServer>();
        string token = (await server.BeginTransactionAsync()).TransactionId;
        clock.Advance(TimeSpan.FromMinutes(11));
        if (diagnosticsEnabled) { await completed.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        var eviction = Assert.Single(logs.Entries, e => Equals(e.Properties.GetValueOrDefault("Reason"), "TransactionEvicted"));
        Assert.Equal(LogLevel.Warning, eviction.Level);
        Assert.DoesNotContain(token, eviction.Message);
        Assert.DoesNotContain("rolled back", eviction.Message);
        await server.RollbackTransactionAsync(token);
        Assert.DoesNotContain(logs.Entries, e => e.Level == LogLevel.Error);
    }


    [Fact]
    public async Task Container_shutdown_waits_for_evicted_cleanup_before_disposing_its_logger()
    {
        var logs = new FailureLogs();
        var clock = new FakeTimeProvider();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interceptor = new FailingTransactions { PauseRollback = release.Task };
        var services = new ServiceCollection().AddMetrics().AddInfoCarrierServerDiagnostics()
            .AddInfoCarrierServerTransactionTimeout(TimeSpan.FromMinutes(10));
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<ILoggerFactory>(_ => new OwnedLoggerFactory(logs));
        services.AddDbContext<Northwind.Shared.NorthwindContext>(b => b.UseSqlite("Data Source=:memory:").AddInterceptors(interceptor));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<Northwind.Shared.NorthwindContext>());
        services.AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>();
        services.AddSingleton(_ => new AdvanceOnDispose(clock));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var server = provider.GetRequiredService<IInfoCarrierServer>();
        await server.BeginTransactionAsync();
        _ = provider.GetRequiredService<AdvanceOnDispose>();
        Task shutdown = provider.DisposeAsync().AsTask();
        Assert.Equal(1, interceptor.Rollbacks);
        try { Assert.False(shutdown.IsCompleted); }
        finally { release.TrySetResult(); }
        await shutdown.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(logs.Entries, e => Equals(e.Properties.GetValueOrDefault("Reason"), "CleanupRollbackFailure"));
        Assert.Single(logs.Entries, e => Equals(e.Properties.GetValueOrDefault("Reason"), "CleanupTransactionDisposalFailure"));
    }

    private sealed class AdvanceOnDispose(FakeTimeProvider clock) : IDisposable
    {
        public void Dispose() => clock.Advance(TimeSpan.FromMinutes(11));
    }

    private sealed class OwnedLoggerFactory : ILoggerFactory
    {
        private readonly LifetimeLogs _provider;
        private readonly ILoggerFactory _factory;
        public OwnedLoggerFactory(FailureLogs logs)
        {
            _provider = new LifetimeLogs(logs);
            _factory = LoggerFactory.Create(b => b.AddProvider(_provider));
        }
        public ILogger CreateLogger(string categoryName) => _factory.CreateLogger(categoryName);
        public void AddProvider(ILoggerProvider provider) => _factory.AddProvider(provider);
        public void Dispose()
        {
            _provider.Dispose();
            _factory.Dispose();
        }
    }

    private sealed class LifetimeLogs(FailureLogs sink) : ILoggerProvider
    {
        private bool _disposed;
        public ILogger CreateLogger(string categoryName) => new LifetimeLogger(this, sink.CreateLogger(categoryName));
        public void Dispose() => _disposed = true;
        private sealed class LifetimeLogger(LifetimeLogs owner, ILogger inner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => !owner._disposed;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (owner._disposed) { throw new ObjectDisposedException(nameof(LifetimeLogs)); }
                inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }

    private sealed class FailingScope(Action disposed) : IAsyncDisposable, IDisposable
    {
        public void Dispose() { }
        public ValueTask DisposeAsync()
        {
            disposed();
            throw new InvalidOperationException("secret-scope-disposal");
        }
    }

    private sealed class FailingTransactions : DbTransactionInterceptor
    {
        public Task? PauseRollback { get; init; }
        public int Rollbacks;
        public int Disposals;
        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult<DbTransaction>.SuppressWithResult(new FailingTransaction(connection, this)));
    }

    private sealed class FailingTransaction(DbConnection connection, FailingTransactions owner) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.Serializable;
        protected override DbConnection? DbConnection => connection;
        public override void Commit() { }
        public override void Rollback() => throw new InvalidOperationException("secret-rollback");
        public override async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref owner.Rollbacks);
            if (owner.PauseRollback is { } pause) { await pause; }
            throw new InvalidOperationException("secret-rollback");
        }
        public override ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref owner.Disposals);
            throw new InvalidOperationException("secret-transaction-disposal");
        }
    }
}
