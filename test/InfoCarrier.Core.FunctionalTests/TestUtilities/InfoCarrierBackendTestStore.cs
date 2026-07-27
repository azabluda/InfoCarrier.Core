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

        ServiceProvider = AddServices(new ServiceCollection().AddLogging())
            .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
            .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
            .AddSingleton(TestModelSource.GetFactory(_testStoreProperties.OnModelCreating!))
            .AddDbContext(
                _testStoreProperties.ContextType,
                (s, b) => AddProviderOptions(b),
                ServiceLifetime.Transient,
                ServiceLifetime.Singleton)
            .AddScoped<DbContext>(sp => (DbContext)sp.GetRequiredService(_testStoreProperties.ContextType))
            .BuildServiceProvider(validateScopes: true);

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
        => (DbContext)ServiceProvider.GetRequiredService(_testStoreProperties.ContextType);

    /// <summary>
    ///     Adds the backend provider services (e.g. InMemory, SqlServer) to the collection.
    /// </summary>
    protected abstract IServiceCollection AddServices(IServiceCollection serviceCollection);

    /// <inheritdoc />
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => _testStoreProperties.OnAddOptions?.Invoke(builder.UseInternalServiceProvider(ServiceProvider))
            ?? builder.UseInternalServiceProvider(ServiceProvider);

    /// <inheritdoc />
    public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
        => await RoundTripAsync(
            request,
            (r, ct) => CreateServer().QueryDataAsync(r, ct),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default)
        => await RoundTripAsync(
            request,
            (r, ct) => CreateServer().SaveChangesAsync(r, ct),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => await CreateServer().BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => CreateServer().CommitTransactionAsync(transactionId, cancellationToken);

    /// <inheritdoc />
    public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => CreateServer().RollbackTransactionAsync(transactionId, cancellationToken);

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
