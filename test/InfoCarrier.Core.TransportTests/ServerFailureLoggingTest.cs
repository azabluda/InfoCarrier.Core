// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using InfoCarrier.Core.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using InfoCarrier.Core.Expressions;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class ServerFailureLoggingTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task Missing_shape_registration_is_logged_as_configuration_without_payload_values()
    {
        var logs = new FailureLogs();
        await using var host = factory.WithWebHostBuilder(b => b
            .ConfigureLogging(l => l.AddProvider(logs))
            .ConfigureServices(s => s.RemoveAll<AnonymousShapeCatalog>()));
        using var context = NorthwindOverHttpTest.CreateClientContext(host);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Orders.Select(o => new { o.Id, o.CustomerId }).ToListAsync());
        FailureLogs.Entry entry = Assert.Single(logs.Entries);
        Assert.Equal("Configuration", entry.Properties["Reason"]);
        Assert.Equal("Rebinding", entry.Properties["Phase"]);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("CustomerId", entry.Message);
    }

    [Theory]
    [InlineData("version", "ProtocolVersion", HttpStatusCode.BadRequest)]
    [InlineData("operation", "UnknownOperation", HttpStatusCode.OK)]
    [InlineData("body", "InvalidPayload", HttpStatusCode.BadRequest)]
    public async Task Refusals_have_one_safe_server_event(string scenario, string reason, HttpStatusCode status)
    {
        var logs = new FailureLogs();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));
        using HttpClient client = host.CreateClient();
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        byte[] body = scenario == "body" ? Encoding.UTF8.GetBytes("secret-invalid-input\n") : serializer.Serialize(new InfoCarrierEnvelope
        {
            ProtocolVersion = scenario == "version" ? 999 : InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = (InfoCarrierOperation)999,
            CorrelationId = "secret-client-correlation\n",
            Payload = serializer.Serialize<object?>(null),
        });
        using var response = await client.PostAsync("infocarrier", new ByteArrayContent(body));
        Assert.Equal(status, response.StatusCode);
        FailureLogs.Entry entry = Assert.Single(logs.Entries);
        Assert.Equal(reason, entry.Properties["Reason"]);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.All(entry.Properties.Values, value => Assert.DoesNotContain("secret", value?.ToString() ?? ""));
        Assert.False(string.IsNullOrEmpty(entry.Properties["RequestId"]?.ToString()));
    }
    [Theory]
    [InlineData("inner", "InvalidPayload", "Input")]
    [InlineData("query", "InvalidPayload", "Validation")]
    [InlineData("descriptor", "InvalidDescriptor", "Rebinding")]
    [InlineData("type", "Permission", "Rebinding")]
    [InlineData("operator", "InvalidDescriptor", "Rebinding")]
    [InlineData("method", "Permission", "Rebinding")]
    public async Task Invalid_query_inputs_have_bounded_reasons(string scenario, string reason, string phase)
    {
        var logs = new FailureLogs();
        await using var host = factory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs))
            .ConfigureServices(s => s.AddInfoCarrierAllowedTypes(typeof(RefusedMethods))));
        using HttpClient client = host.CreateClient();
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        ExpressionNode node = new ConstantNode { Type = new TypeNode
        {
            Name = scenario == "descriptor" ? "" : typeof(System.IO.FileInfo).FullName!,
        } };
        if (scenario == "operator")
        {
            var type = new TypeNode { Name = typeof(int).FullName! };
            node = new UnaryNode { Operator = "secret-invalid-operator", Type = type,
                Operand = new ConstantNode { Type = type, PrimitiveValue = 1 } };
        }
        if (scenario == "method")
        {
            var type = new TypeNode { Name = typeof(int).FullName! };
            node = new MethodCallNode { Type = type, Method = new MethodNode
            {
                DeclaringType = new TypeNode { Name = typeof(RefusedMethods).FullName! },
                Name = RefusedMethods.Name, ReturnType = type,
            } };
        }
        byte[] query = scenario == "query" ? Encoding.UTF8.GetBytes("secret-invalid-query")
            : JsonSerializer.SerializeToUtf8Bytes(node, ExpressionJsonContext.Default.ExpressionNode);
        var envelope = new InfoCarrierEnvelope
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = InfoCarrierOperation.Query,
            CorrelationId = "secret-correlation\n",
            Payload = scenario == "inner" ? Encoding.UTF8.GetBytes("secret-invalid-inner") : serializer.Serialize(new QueryDataRequest
            {
                SerializedQuery = query, TrackingBehavior = QueryTrackingBehavior.NoTracking,
                IsAsync = true, ReturnsSingleResult = true,
            }),
        };
        using var response = await client.PostAsync("infocarrier", new ByteArrayContent(serializer.Serialize(envelope)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(serializer.Deserialize<InfoCarrierEnvelope>(await response.Content.ReadAsByteArrayAsync())!.Fault);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(reason, entry.Properties["Reason"]);
        Assert.Equal(phase, entry.Properties["Phase"]);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.DoesNotContain("FileInfo", entry.Message);
    }

    [Theory]
    [InlineData("missing", "TransactionNotOpen")]
    [InlineData("instance", "WrongInstance")]
    [InlineData("caller", "CallerMismatch")]
    public async Task Transaction_refusals_exclude_bearer_tokens(string scenario, string reason)
    {
        var logs = new FailureLogs();
        var caller = new CallerIdentity();
        await using var host = factory.WithWebHostBuilder(b => b
            .ConfigureLogging(l => l.AddProvider(logs))
            .ConfigureServices(s => s.AddSingleton<IInfoCarrierServerCallerIdentity>(caller)));
        using HttpClient client = host.CreateClient();
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var server = host.Services.GetRequiredService<IInfoCarrierServer>();
        string token = scenario == "caller" ? (await server.BeginTransactionAsync()).TransactionId
            : scenario == "instance" ? "secret-instance.secret-token" : "secret-missing-token";
        caller.CurrentCallerId = "secret-other-caller";
        try
        {
            var envelope = new InfoCarrierEnvelope
            {
                ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
                Operation = InfoCarrierOperation.CommitTransaction,
                Payload = serializer.Serialize(token),
            };
            using var response = await client.PostAsync("infocarrier", new ByteArrayContent(serializer.Serialize(envelope)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(serializer.Deserialize<InfoCarrierEnvelope>(await response.Content.ReadAsByteArrayAsync())!.Fault);
            var entry = Assert.Single(logs.Entries);
            Assert.Equal(reason, entry.Properties["Reason"]);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.DoesNotContain(token, entry.Message);
            Assert.DoesNotContain("secret", entry.Message);
        }
        finally
        {
            caller.CurrentCallerId = "secret-owner";
            await server.RollbackTransactionAsync(token);
        }
    }

    [Fact]
    public async Task Oversized_outer_envelopes_keep_http_400_and_log_the_limit()
    {
        var logs = new FailureLogs();
        await using var host = factory.WithWebHostBuilder(b => b
            .ConfigureLogging(l => l.AddProvider(logs))
            .ConfigureServices(s => s.AddSingleton<IInfoCarrierSerializer>(
                new SystemTextJsonInfoCarrierSerializer(new InfoCarrierPayloadLimits(maxRequestBytes: 32)))));
        using var client = host.CreateClient();
        using var response = await client.PostAsync("infocarrier", new ByteArrayContent(new byte[33]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal("PayloadLimit", entry.Properties["Reason"]);
        Assert.Equal(LogLevel.Information, entry.Level);
    }


    [Theory]
    [InlineData(false, "ExecutionFailure")]
    [InlineData(true, "Database")]
    public async Task Real_query_pipeline_reports_translation_and_database_failures(bool databaseFailure, string reason)
    {
        var logs = new FailureLogs();
        await using var host = factory.WithWebHostBuilder(b => b
            .ConfigureLogging(l => l.AddProvider(logs))
            .ConfigureServices(s =>
            {
                if (databaseFailure) { s.AddSingleton<IInterceptor>(new FailingCommand()); }
            }));
        using var context = NorthwindOverHttpTest.CreateClientContext(host);
        if (databaseFailure)
        {
            await Assert.ThrowsAsync<InfoCarrierServerException>(() => context.Customers.ToListAsync());
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Customers
                .Where(c => c.Id.StartsWith("secret-expression", StringComparison.Ordinal)).ToListAsync());
        }
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(reason, entry.Properties["Reason"]);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("secret", entry.Message);
    }

    [Fact]
    public async Task Final_envelope_serialization_failure_identifies_the_completed_operation()
    {
        var logs = new FailureLogs();
        var server = new EnvelopeFailureLoggingTest.TestServer(null);
        await using var host = factory.WithWebHostBuilder(b => b
            .ConfigureLogging(l => l.AddProvider(logs))
            .ConfigureServices(s =>
            {
                s.AddSingleton<IInfoCarrierServer>(server);
                s.AddSingleton<IInfoCarrierSerializer>(new FinalEnvelopeFailure());
            }));
        using var client = host.CreateClient();
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var envelope = new InfoCarrierEnvelope
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = InfoCarrierOperation.BeginTransaction,
            Payload = serializer.Serialize<object?>(null),
        };
        await Assert.ThrowsAsync<FormatException>(() => client.PostAsync("infocarrier", new ByteArrayContent(serializer.Serialize(envelope))));
        Assert.Equal(1, server.Completed);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal("ResponseSerialization", entry.Properties["Reason"]);
        Assert.Equal("EnvelopeSerialization", entry.Properties["Phase"]);
        Assert.Equal("BeginTransaction", entry.Properties["Operation"]);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("secret", entry.Message);
    }


    [Fact]
    public async Task Save_changes_concurrency_conflicts_are_information_events()
    {
        var logs = new FailureLogs();
        await using var host = factory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));
        using var context = NorthwindOverHttpTest.CreateClientContext(host);
        context.Remove(new Northwind.Shared.Model.Customer
        {
            Id = "secret-missing-customer", CompanyName = "secret-company", City = "secret-city", Country = "secret-country",
        });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync());
        var entry = Assert.Single(logs.Entries);
        Assert.Equal("Concurrency", entry.Properties["Reason"]);
        Assert.Equal("SaveChanges", entry.Properties["Operation"]);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.Null(entry.Exception);
    }

    private sealed class FinalEnvelopeFailure : IInfoCarrierSerializer
    {
        private readonly SystemTextJsonInfoCarrierSerializer _inner = new();
        public byte[] Serialize<T>(T value) => value is InfoCarrierEnvelope
            ? throw new FormatException("secret-final-serialization") : _inner.Serialize(value);
        public T? Deserialize<T>(byte[] payload) => _inner.Deserialize<T>(payload);
        public ValueTask<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default) => ValueTask.FromResult(Serialize(value));
        public ValueTask<T?> DeserializeAsync<T>(byte[] payload, CancellationToken cancellationToken = default) => ValueTask.FromResult(Deserialize<T>(payload));
    }

    private sealed class FailingCommand : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            => throw new SqliteException("secret-database", 1);
    }

    public static class RefusedMethods
    {
        public static string Name => nameof(Secret);
        private static int Secret() => 1;
    }

    private sealed class CallerIdentity : IInfoCarrierServerCallerIdentity
    {
        public string? CurrentCallerId { get; set; } = "secret-owner";
    }

}

internal sealed class FailureLogs(Action<FailureLogs.Entry>? recorded = null) : ILoggerProvider
{
    public sealed record Entry(LogLevel Level, EventId Id, string Message, Exception? Exception, Dictionary<string, object?> Properties);
    public ConcurrentQueue<Entry> Entries { get; } = new();
    private Action<Entry>? Recorded { get; } = recorded;
    public ILogger CreateLogger(string categoryName) => new Recorder(this, categoryName);
    public void Dispose() { }
    private sealed class Recorder(FailureLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (category == "InfoCarrier.Server")
            {
                var entry = new Entry(logLevel, eventId, formatter(state, exception), exception,
                    state is IEnumerable<KeyValuePair<string, object?>> values ? values.ToDictionary(v => v.Key, v => v.Value) : []);
                owner.Entries.Enqueue(entry);
                owner.Recorded?.Invoke(entry);
            }
        }
    }
}
