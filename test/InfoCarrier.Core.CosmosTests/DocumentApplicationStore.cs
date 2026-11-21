// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications;

/// <summary>Isolated application contexts with explicit backend deployment configuration.</summary>
internal sealed class DocumentApplicationStore<TContext>(Func<DbContextOptions, TContext> create) : IAsyncDisposable
    where TContext : DbContext
{
    private readonly string _database = "icc-application-" + Guid.NewGuid().ToString("N");
    private readonly List<ServiceProvider> _providers = [];
    private readonly List<InProcessInfoCarrierServer> _servers = [];
    private DbContextOptions? _options;
    public ListLoggerFactory Logger { get; } = new(c => c == DbLoggerCategory.Database.Command.Name);
    public InfoCarrierPayloadLimits PayloadLimits { get; set; } = InfoCarrierPayloadLimits.Default;
    public IReadOnlyList<Type> AllowedTypes { get; init; } = [];
    public AutoTransactionBehavior TransactionMode { get; set; } = AutoTransactionBehavior.WhenNeeded;
    public Action<DbContextOptionsBuilder>? ConfigureBackend { get; init; }

    public async ValueTask InitializeAsync()
    {
        await CosmosEmulator.EnsureAvailableAsync(TestContext.Current.CancellationToken);
        CosmosEmulator.ReportDatabaseOwner(_database, this, typeof(TContext).FullName!);
        var builder = CosmosEmulator.Configure(new DbContextOptionsBuilder(), _database)
            .UseLoggerFactory(Logger)
            .ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined));
        ConfigureBackend?.Invoke(builder);
        _options = builder.Options;
        using var backend = create(_options);
        await backend.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public TContext CreateContext(Action<DbContextOptionsBuilder>? configureBackend = null)
    {
        var builder = new DbContextOptionsBuilder(_options!);
        configureBackend?.Invoke(builder);
        builder.AddInterceptors(new BackendMode(() => TransactionMode));
        var options = builder.Options;
        var services = new ServiceCollection().AddScoped<DbContext>(_ => create(options));
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        var serverOptions = new InfoCarrierServerOptions().WithStoresOwnedDataInsideOwner(true);
        serverOptions.AddAllowedTypes([typeof(CosmosQueryableExtensions), .. AllowedTypes]);
        var server = new InProcessInfoCarrierServer(provider, serverOptions);
        _servers.Add(server);
        var serializer = new SystemTextJsonInfoCarrierSerializer(PayloadLimits);
        var envelope = new InfoCarrierEnvelopeServer(server, serializer);
        var transport = new InfoCarrier.Core.FunctionalTests.TestUtilities.InProcessInfoCarrierTransport(
            envelope.Dispatch, envelope.DispatchAsync, serializer);
        using var backend = create(options);
        var clientOptions = new DbContextOptionsBuilder().UseInfoCarrier(
            new TransportInfoCarrierClient(transport, serializer), o =>
            {
                o.ServerUsesRelationalQueryRules = false;
                o.AddAllowedTypes([typeof(CosmosQueryableExtensions), .. AllowedTypes]);
            }).UseModel(backend.Model).Options;
        return create(clientOptions);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_options is not null)
                await CosmosEmulator.RunDatabaseCleanupAsync(async () =>
                {
                    using var backend = create(_options);
                    await backend.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                }, _database, typeof(TContext).FullName!, CancellationToken.None);
        }
        finally
        {
            foreach (var server in _servers) await server.DisposeAsync();
            foreach (var provider in _providers) await provider.DisposeAsync();
            Logger.Dispose();
        }
    }

    private sealed class BackendMode(Func<AutoTransactionBehavior> mode) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            eventData.Context!.Database.AutoTransactionBehavior = mode();
            return result;
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            eventData.Context!.Database.AutoTransactionBehavior = mode();
            return ValueTask.FromResult(result);
        }
    }
}
