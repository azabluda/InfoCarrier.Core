// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The backend test store: an EF spec-test <see cref="TestStore" /> that doubles as the
///     <see cref="IInfoCarrierClient" /> the client provider talks to (v1's
///     <c>InfoCarrierBackendTestStore</c> pattern, rebuilt for EF Core 10).
/// </summary>
/// <remarks>
///     Two <see cref="IServiceProvider" />s exist: the client provider (the spec test's
///     context) and this store's server provider. Every request and result round-trips
///     through the configured <see cref="IInfoCarrierSerializer" /> in-process, so
///     wire-serializability failures surface exactly as over a network.
/// </remarks>
public abstract class InfoCarrierBackendTestStore : TestStore, IInfoCarrierClient
{
    private readonly IInfoCarrierServer _server;
    private readonly IInfoCarrierSerializer _serializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierBackendTestStore" /> class.
    /// </summary>
    protected InfoCarrierBackendTestStore(string name, bool shared)
        : base(name, shared)
    {
        ServiceProvider = AddServices(new ServiceCollection())
            .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
            .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
            .BuildServiceProvider(validateScopes: true);

        _server = ServiceProvider.GetRequiredService<IInfoCarrierServer>();
        _serializer = ServiceProvider.GetRequiredService<IInfoCarrierSerializer>();
    }

    /// <summary>
    ///     The server URL/name this store stands in for.
    /// </summary>
    public string ServerUrl => Name;

    /// <summary>
    ///     Adds the backend provider services (e.g. InMemory, SqlServer) to the collection.
    /// </summary>
    protected abstract IServiceCollection AddServices(IServiceCollection serviceCollection);

    /// <inheritdoc />
    public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
        => await RoundTripAsync(
            request,
            (r, ct) => _server.QueryDataAsync(r, ct),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default)
        => await RoundTripAsync(
            request,
            (r, ct) => _server.SaveChangesAsync(r, ct),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => await _server.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => _server.CommitTransactionAsync(transactionId, cancellationToken);

    /// <inheritdoc />
    public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => _server.RollbackTransactionAsync(transactionId, cancellationToken);

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
