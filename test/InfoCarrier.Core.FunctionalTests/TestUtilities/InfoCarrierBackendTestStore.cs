// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The backend test store: an EF spec-test <see cref="TestStore" /> that doubles as the
///     <see cref="IInfoCarrierClient" /> the client provider talks to (v1's
///     <c>InfoCarrierBackendTestStore</c> pattern, rebuilt for EF Core 10).
/// </summary>
/// <remarks>
///     Two <see cref="IServiceProvider" />s exist: the client provider (built by the spec-test
///     fixture) and this store's <em>server</em> provider (built eagerly here, with a
///     <c>TestModelSource</c> for the server model and the server <see cref="DbContext" />).
///     Every request and result round-trips through the configured
///     <see cref="IInfoCarrierSerializer" /> in-process, so wire-serializability failures
///     surface exactly as over a network (v1's <c>SimulateNetworkTransferJson</c>).
/// </remarks>
public abstract class InfoCarrierBackendTestStore : TestStore, IInfoCarrierClient
{
    private readonly SharedTestStoreProperties _testStoreProperties;
    private readonly IInfoCarrierSerializer _serializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierBackendTestStore" /> class,
    ///     building the server service provider eagerly.
    /// </summary>
    protected InfoCarrierBackendTestStore(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties)
        : base(name, shared)
    {
        _testStoreProperties = testStoreProperties;

        IServiceCollection services = AddServices(new ServiceCollection().AddLogging());
        if (testStoreProperties.OnAddServices is { } onAddServices)
        {
            services = onAddServices(services);
        }

        services = services
            .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
            .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
            .AddSingleton(TestModelSource.GetFactory(_testStoreProperties.OnModelCreating!))
            .AddDbContext(
                ServerContextType,
                (s, b) => AddProviderOptions(b),
                ServiceLifetime.Transient,
                ServiceLifetime.Singleton);

        // Only when the fixture's context is a *subclass*. A spec fixture may be
        // `SharedStoreFixtureBase<DbContext>` — `LazyLoadProxyTestBase`'s is — and then this
        // registration would re-register `DbContext` itself as scoped, overriding the transient
        // one `AddDbContext` just made and making it unresolvable from the root provider:
        // "cannot resolve scoped service 'DbContext' from root provider", every test in the
        // class failing identically before any of them ran.
        if (ServerContextType != typeof(DbContext))
        {
            SharedTestStoreProperties props = _testStoreProperties;
            services = services.AddScoped<DbContext>(sp =>
            {
                var serverContext = (DbContext)sp.GetRequiredService(ServerContextType);

                // The one place a request's client context and its server context are both in
                // hand. `CopyDbContextParameters` had been declared and assigned since the
                // factory was written and never invoked, which is why three query-filter tests
                // read the server's default tenant instead of the client's.
                if (CurrentClientContext.Value is { } clientContext)
                {
                    props.CopyDbContextParameters?.Invoke(clientContext, serverContext);
                }

                return serverContext;
            });
        }

        ServiceProvider = services.BuildServiceProvider(validateScopes: true);

        _serializer = ServiceProvider.GetRequiredService<IInfoCarrierSerializer>();
    }

    /// <summary>
    ///     The server URL/name this store stands in for.
    /// </summary>
    public string ServerUrl => Name;

    /// <summary>
    ///     Creates a server-side <see cref="DbContext" /> from the server provider.
    /// </summary>
    public virtual DbContext CreateDbContext()
        => (DbContext)ServiceProvider.GetRequiredService(ServerContextType);

    /// <summary>
    ///     The context type the server runs, which may add store-specific model configuration
    ///     the client neither has nor needs (e.g. defining queries for keyless entity types).
    /// </summary>
    private Type ServerContextType
        => _testStoreProperties.ServerContextType ?? _testStoreProperties.ContextType;

    /// <summary>
    ///     Adds the backend provider services (e.g. InMemory, SqlServer) to the collection.
    /// </summary>
    protected abstract IServiceCollection AddServices(IServiceCollection serviceCollection);

    /// <summary>
    ///     Builds the server context's options.
    /// </summary>
    /// <remarks>
    ///     <see cref="EnableSensitiveDataLogging" /> because the spec fixtures set it on the
    ///     client (<c>FixtureBase.AddOptions</c>) and an exception raised while the *server*
    ///     compiles the same query should read the same way. Without it,
    ///     <c>Local_variable_from_OnModelCreating_can_throw_exception</c> got EF's message minus
    ///     the expression that caused it — right exception, right place, two words different.
    ///     Deliberately not the rest of <c>AddOptions</c>: its
    ///     <c>ConfigureWarnings(Default(Throw))</c> is a statement about what the test author
    ///     wrote, and the server runs a tree this provider generated.
    /// </remarks>
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
    {
        builder = builder.UseInternalServiceProvider(ServiceProvider).EnableSensitiveDataLogging();

        return _testStoreProperties.OnAddOptions?.Invoke(builder) ?? builder;
    }

    /// <inheritdoc />
    public async Task<QueryDataResult> QueryDataAsync(
        QueryDataRequest request,
        DbContext clientContext,
        CancellationToken cancellationToken = default)
    {
        using var _ = WithClientContext(clientContext);
        return await RoundTripAsync(
            request,
            (r, ct) => CreateServer().QueryDataAsync(r, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SaveChangesResult> SaveChangesAsync(
        SaveChangesRequest request,
        DbContext clientContext,
        CancellationToken cancellationToken = default)
    {
        using var _ = WithClientContext(clientContext);
        return await RoundTripAsync(
            request,
            (r, ct) => CreateServer().SaveChangesAsync(r, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     The client context of the request currently in flight, for the server context factory
    ///     below to copy per-request parameters from.
    /// </summary>
    /// <remarks>
    ///     A context property the model reads — <c>NorthwindContext.TenantPrefix</c>, which a
    ///     query filter closes over — never reaches the wire: the client captures its tree before
    ///     EF applies query filters, and the *server* applies its own model's filter using its
    ///     own context. So the value has to be carried out of band. `AsyncLocal` because the
    ///     server is a singleton serving concurrent requests, and the scope is one request.
    /// </remarks>
    private static readonly AsyncLocal<DbContext?> CurrentClientContext = new();

    private static IDisposable WithClientContext(DbContext clientContext)
    {
        DbContext? previous = CurrentClientContext.Value;
        CurrentClientContext.Value = clientContext;
        return new Restore(() => CurrentClientContext.Value = previous);
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    /// <inheritdoc />
    public async Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => await CreateServer().BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => CreateServer().CommitTransactionAsync(transactionId, cancellationToken);

    /// <inheritdoc />
    public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => CreateServer().RollbackTransactionAsync(transactionId, cancellationToken);

    /// <inheritdoc />
    public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => CreateServer().CreateSavepointAsync(transactionId, name, cancellationToken);

    /// <inheritdoc />
    public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => CreateServer().RollbackToSavepointAsync(transactionId, name, cancellationToken);

    /// <inheritdoc />
    public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => CreateServer().ReleaseSavepointAsync(transactionId, name, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
        => CreateServer().SupportsSavepointsAsync(transactionId, cancellationToken);

    private IInfoCarrierServer CreateServer()
        => ServiceProvider.GetRequiredService<IInfoCarrierServer>();

    private async Task<TResponse> RoundTripAsync<TRequest, TResponse>(
        TRequest request,
        Func<TRequest, CancellationToken, Task<TResponse>> invoke,
        CancellationToken cancellationToken)
    {
        // Simulate the wire: serialize the request, deserialize a fresh copy, invoke the
        // server, then round-trip the result back (v1 SimulateNetworkTransferJson).
        TRequest simulatedRequest = await SimulateAsync(request, cancellationToken).ConfigureAwait(false);
        TResponse result = await invoke(simulatedRequest, cancellationToken).ConfigureAwait(false);
        return await SimulateAsync(result, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> SimulateAsync<T>(T value, CancellationToken cancellationToken)
    {
        byte[] payload = await _serializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);
        return (await _serializer.DeserializeAsync<T>(payload, cancellationToken).ConfigureAwait(false))!;
    }
}
