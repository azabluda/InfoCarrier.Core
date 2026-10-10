// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using System.Linq.Expressions;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

[Collection(nameof(RoundTripMetricsCollection))]
public class ClientProcessingLoggingTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Local_failure_reaches_simple_logging_and_diagnostic_source(bool save)
    {
        using var fixture = new Fixture();
        int id = save ? 35102 : 35101;
        List<string> simple = [];
        List<EventData> diagnostic = [];
        List<IDisposable> subscriptions = [];
        using var listeners = DiagnosticListener.AllListeners.Subscribe(new Observer<DiagnosticListener>(listener =>
        {
            if (listener.Name == "Microsoft.EntityFrameworkCore")
            {
                subscriptions.Add(listener.Subscribe(new Observer<KeyValuePair<string, object?>>(entry =>
                {
                    if (entry.Value is EventData data && data.EventId.Id == id) { diagnostic.Add(data); }
                }), name => name.EndsWith(save ? ".ClientSaveFailure" : ".ClientQueryFailure", StringComparison.Ordinal)));
            }
        }));
        try
        {
            using var context = fixture.CreateClient(configure: b =>
            {
                b.LogTo(simple.Add, (eventId, _) => eventId.Id == id);
                if (save) { b.ReplaceService<IExpressionSerializer, FailingChangeSerializer>(); }
            });
            if (save) { context.Items.Add(new Item { Name = "secret" }); }
            Assert.NotNull(await Record.ExceptionAsync(async () =>
            {
                if (save) { await context.SaveChangesAsync(); }
                else { await context.Items.Select(x => Fail(x.Name)).ToListAsync(); }
            }));
            Assert.DoesNotContain("secret", Assert.Single(simple));
            var payload = Assert.IsType<ClientFailureEventData>(Assert.Single(diagnostic));
            Assert.DoesNotContain("secret", payload.ToString());
            Assert.Equal(save ? "SaveChanges" : "Query", payload.Operation);
            Assert.Equal(typeof(InvalidOperationException).FullName, payload.ExceptionType);
            Assert.Equal("UnclassifiedFailure", payload.Outcome);
            Assert.Single(fixture.Log.Entries, e => e.Id == id);
        }
        finally { foreach (IDisposable subscription in subscriptions) { subscription.Dispose(); } }
    }

    [Fact]
    public void Warning_configuration_can_disable_provider_failure_logging()
    {
        using var fixture = new Fixture();
        List<string> simple = [];
        using var context = fixture.CreateClient(configure: b => b
            .ConfigureWarnings(w => w.Ignore(new EventId(35101)))
            .LogTo(simple.Add, (id, _) => id.Id == 35101));
        Assert.Throws<InvalidOperationException>(() => context.Items.Select(x => Fail(x.Name)).ToList());
        Assert.Empty(simple);
        Assert.DoesNotContain(fixture.Log.Entries, e => e.Id == 35101);
    }

    [Fact]
    public void Simple_logging_works_without_an_application_logger_factory()
    {
        using var fixture = new Fixture();
        List<string> messages = [];
        using var context = new Context(new DbContextOptionsBuilder<Context>().UseInfoCarrier(fixture.Client)
            .LogTo(messages.Add, (id, _) => id.Id == 35101).Options);
        Assert.Throws<InvalidOperationException>(() => context.Items.Select(x => Fail(x.Name)).ToList());
        Assert.DoesNotContain("secret", Assert.Single(messages));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Throwing_simple_logger_or_warning_policy_preserves_original_failure(bool warningThrows)
    {
        using var fixture = new Fixture();
        using var context = fixture.CreateClient(configure: b =>
        {
            if (warningThrows) { b.ConfigureWarnings(w => w.Throw(InfoCarrierEventId.ClientQueryFailure)); }
            else { b.LogTo(_ => throw new InvalidOperationException("logger"), (id, _) => id.Id == 35101); }
        });
        var failure = Assert.Throws<InvalidOperationException>(() => context.Items.Select(x => Fail(x.Name)).ToList());
        Assert.Equal("secret-projection", failure.Message);
    }

    private sealed class Observer<T>(Action<T> next) : IObserver<T>
    {
        public void OnNext(T value) => next(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Projection_failure_after_successful_exchange_has_safe_local_event(bool async)
    {
        using var fixture = new Fixture();
        using var context = fixture.CreateClient();
        IQueryable<string> query = context.Items.OrderBy(x => x.Id).Select(x => Fail(x.Name));

        Exception? failure = await Record.ExceptionAsync(async () =>
        {
            if (async) { await query.ToListAsync(); }
            else { query.ToList(); }
        });

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal("secret-projection", failure.Message);
        Logs.Entry entry = Assert.Single(fixture.Log.Entries, x => x.Id == 35101);
        Assert.Contains("client projection", entry.Message);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public void Preparation_refusal_has_informational_event_without_changing_the_exception()
    {
        using var fixture = new Fixture();
        using var context = fixture.CreateClient();
        Assert.Throws<InvalidOperationException>(() => context.Items.Where(x => Fail(x.Name) == "x").ToList());
        Logs.Entry entry = Assert.Single(fixture.Log.Entries, x => x.Id == 35101);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("query preparation", entry.Message);
        Assert.Null(entry.Exception);
    }

    private static string Fail(string value) => throw new InvalidOperationException("secret-projection");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Later_projection_failure_logs_once_when_the_row_is_requested(bool async)
    {
        using var fixture = new Fixture();
        using (var seed = fixture.CreateClient())
        {
            seed.Items.Add(new Item { Id = 2, Name = "two" });
            await seed.SaveChangesAsync();
        }
        using var context = fixture.CreateClient();
        var query = context.Items.OrderBy(x => x.Id).Select(x => x.Id == 1 ? x.Name : Fail(x.Name));
        if (async)
        {
            await using var enumerator = query.AsAsyncEnumerable().GetAsyncEnumerator();
            Assert.True(await enumerator.MoveNextAsync());
            Assert.DoesNotContain(fixture.Log.Entries, e => e.Id == 35101);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await enumerator.MoveNextAsync());
        }
        else
        {
            using var enumerator = query.AsEnumerable().GetEnumerator();
            Assert.True(enumerator.MoveNext());
            Assert.DoesNotContain(fixture.Log.Entries, e => e.Id == 35101);
            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }
        Assert.Single(fixture.Log.Entries, e => e.Id is 35100 or 35101 or 35102);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Immediate_scalar_projection_failure_logs_once(bool async)
    {
        using var fixture = new Fixture();
        using var context = fixture.CreateClient();
        var query = context.Items.Select(x => Fail(x.Name));
        Assert.NotNull(await Record.ExceptionAsync(async () =>
        {
            if (async) { await query.FirstAsync(); }
            else { query.First(); }
        }));
        Logs.Entry entry = Assert.Single(fixture.Log.Entries, e => e.Id is 35100 or 35101 or 35102);
        Assert.Contains("client projection", entry.Message);
    }

    [Fact]
    public async Task Successful_projection_keeps_existing_split_event_without_failure_event()
    {
        using var fixture = new Fixture();
        using var context = fixture.CreateClient();
        Assert.Equal("ONE", Assert.Single(await context.Items.Select(x => Upper(x.Name)).ToListAsync()));
        Assert.Single(fixture.Log.Entries, e => e.Id == InfoCarrierEventId.QuerySplit.Id && e.Level == LogLevel.Information);
        Assert.DoesNotContain(fixture.Log.Entries, e => e.Id is 35100 or 35101 or 35102);
    }

    private static string Upper(string value) => value.ToUpperInvariant();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Change_mapping_failure_has_one_local_event_before_exchange(bool async)
    {
        using var fixture = new Fixture();
        using var context = fixture.CreateClient(configure: b => b.ReplaceService<IExpressionSerializer, FailingChangeSerializer>());
        context.Items.Add(new Item { Name = "secret-name" });
        Exception? failure = await Record.ExceptionAsync(async () =>
        {
            if (async) { await context.SaveChangesAsync(); }
            else { context.SaveChanges(); }
        });
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal("secret-mapper", failure.Message);
        Logs.Entry entry = Assert.Single(fixture.Log.Entries, e => e.Id is 35100 or 35101 or 35102);
        Assert.Equal(35102, entry.Id);
        Assert.Contains("change request construction", entry.Message);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.Null(entry.Exception);
    }

    public sealed class FailingChangeSerializer(ExpressionToNodeTranslator forward, TypeNodeResolver resolver, IDynamicValueMapper mapper)
        : ExpressionSerializer(forward, resolver, mapper)
    {
        public override IDynamicValueMapper ValueMapper => throw new InvalidOperationException("secret-mapper");
    }

    [Fact]
    public void Shared_compiled_query_uses_each_context_logger_and_host_scope()
    {
        using var fixture = new Fixture();
        var otherLog = new Logs();
        using var otherFactory = LoggerFactory.Create(b => b.AddProvider(otherLog));
        using var first = fixture.CreateClient();
        using var second = fixture.CreateClient(otherFactory);
        Assert.Same(first.Model, second.Model);
        var query = EF.CompileQuery((Context c) => c.Items.Select(x => Fail(x.Name)));
        using (fixture.BeginScope("first"))
        {
            Assert.Throws<InvalidOperationException>(() => query(first).ToList());
        }
        using (otherFactory.CreateLogger("host").BeginScope("second"))
        {
            Assert.Throws<InvalidOperationException>(() => query(second).ToList());
        }
        Logs.Entry firstEvent = Assert.Single(fixture.Log.Entries, x => x.Id == 35101);
        Logs.Entry secondEvent = Assert.Single(otherLog.Entries, x => x.Id == 35101);
        Assert.Contains("first", firstEvent.Scopes);
        Assert.DoesNotContain("second", firstEvent.Scopes);
        Assert.Contains("second", secondEvent.Scopes);
        Assert.DoesNotContain("first", secondEvent.Scopes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Materialization_failure_is_not_relogged_as_projection_or_exchange(bool async)
    {
        using var fixture = new Fixture();
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        fixture.Transform = response => response with { Payload = serializer.Serialize(
            serializer.Deserialize<QueryDataResult>(response.Payload)! with { SerializedResults = [255] }) };
        using var context = fixture.CreateClient();
        Assert.NotNull(await Record.ExceptionAsync(async () =>
        {
            if (async) { await context.Items.ToListAsync(); }
            else { context.Items.ToList(); }
        }));
        Logs.Entry entry = Assert.Single(fixture.Log.Entries, x => x.Id is 35100 or 35101 or 35102);
        Assert.Contains("result materialization", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expression_serialization_failure_has_one_local_observation(bool async)
    {
        using var fixture = new Fixture();
        using var context = fixture.CreateClient(configure: b => b.ReplaceService<IExpressionSerializer, FailingExpressionSerializer>());
        Exception? failure = await Record.ExceptionAsync(async () =>
        {
            if (async) { await context.Items.ToListAsync(); }
            else { context.Items.ToList(); }
        });
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal("secret-expression", failure.Message);
        Logs.Entry entry = Assert.Single(fixture.Log.Entries, x => x.Id is 35100 or 35101 or 35102);
        Assert.Contains("request construction", entry.Message);
        Assert.DoesNotContain("secret", entry.Message);
    }

    public sealed class FailingExpressionSerializer(ExpressionToNodeTranslator forward, TypeNodeResolver resolver, IDynamicValueMapper mapper)
        : ExpressionSerializer(forward, resolver, mapper)
    {
        public override ExpressionNode ToNode(Expression expression, Metadata.IInfoCarrierRelationalQueryRoots? relationalRoots)
            => throw new InvalidOperationException("secret-expression");
    }

    [Fact]
    public void Throwing_local_logger_preserves_projection_failure()
    {
        using var fixture = new Fixture();
        fixture.Log.ThrowEvent = 35101;
        using var context = fixture.CreateClient();
        var failure = Assert.Throws<InvalidOperationException>(() => context.Items.Select(x => Fail(x.Name)).ToList());
        Assert.Equal("secret-projection", failure.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replay_delivery_failure_propagates_and_has_safe_local_phase(bool save)
    {
        using var fixture = new Fixture();
        fixture.Log.ThrowEvent = 39999;
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        ServerLogEvent[] forwarded = [new() { Category = "Test.Forwarded", EventId = 39999, Level = (int)LogLevel.Warning, Message = "secret-replay" }];
        fixture.Transform = response => response with { Payload = save
            ? serializer.Serialize(serializer.Deserialize<SaveChangesResult>(response.Payload)! with { ServerLog = forwarded })
            : serializer.Serialize(serializer.Deserialize<QueryDataResult>(response.Payload)! with { ServerLog = forwarded }) };
        using var context = fixture.CreateClient();
        if (save) { context.Items.Add(new Item { Name = "saved" }); }
        Assert.NotNull(await Record.ExceptionAsync(async () =>
        {
            if (save) { await context.SaveChangesAsync(); }
            else { await context.Items.ToListAsync(); }
        }));
        Logs.Entry entry = Assert.Single(fixture.Log.Entries, x => x.Id is 35100 or 35101 or 35102);
        Assert.Contains("server log replay", entry.Message);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generated_value_failure_does_not_claim_the_server_write_was_rolled_back(bool async)
    {
        using var fixture = new Fixture();
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        fixture.Transform = response => response.Operation == InfoCarrierOperation.SaveChanges
            ? response with { Payload = serializer.Serialize(serializer.Deserialize<SaveChangesResult>(response.Payload)! with
                { GeneratedValues = [new GeneratedValues { CorrelationId = 0, SerializedValues = [255] }] }) }
            : response;
        using var context = fixture.CreateClient();
        context.Items.Add(new Item { Name = "saved" });
        Assert.NotNull(await Record.ExceptionAsync(async () =>
        {
            if (async) { await context.SaveChangesAsync(); }
            else { context.SaveChanges(); }
        }));
        Logs.Entry entry = Assert.Single(fixture.Log.Entries, x => x.Id == 35102);
        Assert.Contains("generated value application", entry.Message);
        Assert.Null(entry.Exception);
        using var verification = fixture.CreateClient();
        Assert.Equal(2, await verification.Items.CountAsync());
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"icc-client-log-{Guid.NewGuid():N}.db");
        private readonly ServiceProvider _services;
        private readonly InProcessInfoCarrierServer _server;
        private readonly ILoggerFactory _factory;
        public Logs Log { get; } = new();
        public IInfoCarrierClient Client { get; }
        public IInfoCarrierTransport Transport { get; }
        public Func<InfoCarrierEnvelope, InfoCarrierEnvelope> Transform { get; set; } = response => response;
        public Func<InfoCarrierEnvelope, InfoCarrierEnvelope> Prepare { get; set; } = request => request;
        public Action<CancellationToken>? Sending { get; set; }

        public Fixture(bool forwarding = false, bool sensitive = false)
        {
            _factory = LoggerFactory.Create(b =>
            {
                b.SetMinimumLevel(LogLevel.Debug).AddProvider(Log);
                if (forwarding) { b.AddProvider(new ServerLogCapture()); }
            });
            var services = new ServiceCollection()
                .AddDbContext<Context>(b => b.UseSqlite($"Data Source={_path};Pooling=false").UseLoggerFactory(_factory).EnableSensitiveDataLogging(sensitive))
                .AddScoped<DbContext>(s => s.GetRequiredService<Context>());
            if (forwarding) { services.AddInfoCarrierServerLogForwarding(LogLevel.Debug); }
            _services = services.BuildServiceProvider();
            using (IServiceScope scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Context>();
                db.Database.EnsureCreated();
                db.Items.Add(new Item { Id = 1, Name = "one" });
                db.SaveChanges();
            }
            var serializer = new SystemTextJsonInfoCarrierSerializer();
            _server = new InProcessInfoCarrierServer(_services, _factory.CreateLogger<InProcessInfoCarrierServer>());
            var dispatcher = new InfoCarrierEnvelopeServer(_server, serializer, _factory.CreateLogger<InfoCarrierEnvelopeServer>());
            Transport = new LocalTransport(dispatcher, response => Transform(response), request => Prepare(request), token => Sending?.Invoke(token));
            Client = new TransportInfoCarrierClient(Transport, serializer,
                _factory.CreateLogger<TransportInfoCarrierClient>());
        }

        public Context CreateClient(ILoggerFactory? factory = null, Action<DbContextOptionsBuilder<Context>>? configure = null)
        {
            var builder = new DbContextOptionsBuilder<Context>().UseInfoCarrier(Client).UseLoggerFactory(factory ?? _factory);
            configure?.Invoke(builder);
            return new(builder.Options);
        }

        public IDisposable? BeginScope(string name) => _factory.CreateLogger("host").BeginScope(name);

        public void Dispose()
        {
            _server.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _services.Dispose();
            _factory.Dispose();
            File.Delete(_path);
        }
    }

    internal sealed class LocalTransport(InfoCarrierEnvelopeServer dispatcher, Func<InfoCarrierEnvelope, InfoCarrierEnvelope> transform,
        Func<InfoCarrierEnvelope, InfoCarrierEnvelope>? prepare = null, Action<CancellationToken>? sending = null) : IInfoCarrierTransport
    {
        public async Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
        {
            sending?.Invoke(cancellationToken);
            return transform(await dispatcher.DispatchAsync(prepare?.Invoke(request) ?? request, cancellationToken));
        }
    }

    internal sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
    }

    internal sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    internal sealed class Logs : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
        public int? ThrowEvent { get; set; }
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
        internal sealed record Entry(int Id, LogLevel Level, string Message, Exception? Exception, string Category, List<string> Scopes);
        public System.Collections.Concurrent.ConcurrentQueue<Entry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);
        public void Dispose() { }
        private sealed class Logger(Logs owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (owner.ThrowEvent == id.Id) { throw new InvalidOperationException("secret-backend"); }
                List<string> scopes = [];
                owner._scopes.ForEachScope((scope, list) => list.Add(scope?.ToString() ?? ""), scopes);
                owner.Entries.Enqueue(new(id.Id, level, formatter(state, exception), exception, category, scopes));
            }
        }
    }
}
