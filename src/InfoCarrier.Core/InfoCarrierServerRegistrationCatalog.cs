// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

internal sealed class InfoCarrierServerRegistrationCatalog
{
    private readonly Dictionary<InfoCarrierRegistrationIdentity, InfoCarrierServerRegistration> _registrations = [];
    private readonly Dictionary<string, InfoCarrierRegistrationIdentity> _names = new(StringComparer.Ordinal);
    private InfoCarrierRegistrationIdentity? _defaultIdentity;

    internal void ValidateName(string? name)
    {
        if (name is not null && _names.ContainsKey(name))
        {
            throw new InvalidOperationException($"An InfoCarrier server named '{name}' is already registered.");
        }
    }

    internal InfoCarrierServerRegistration Add(string? name, InfoCarrierServerRegistration registration)
    {
        ValidateName(name);
        registration = registration with { MetricIdentity = new(name ?? "", _registrations.Count + 1) };
        _registrations.Add(registration.Identity, registration);
        if (name is null) { _defaultIdentity = registration.Identity; }
        else { _names.Add(name, registration.Identity); }
        return registration;
    }

    internal void Replace(InfoCarrierServerRegistration registration)
        => _registrations[registration.Identity] = registration;

    internal InfoCarrierServerRegistrationStore Snapshot() => new(_registrations, _names, _defaultIdentity);
}

internal sealed class InfoCarrierServerRegistrationStore(
    IReadOnlyDictionary<InfoCarrierRegistrationIdentity, InfoCarrierServerRegistration> registrations,
    IReadOnlyDictionary<string, InfoCarrierRegistrationIdentity> names,
    InfoCarrierRegistrationIdentity? defaultIdentity)
{
    private readonly Dictionary<InfoCarrierRegistrationIdentity, InfoCarrierServerRegistration> _registrations = new(registrations);
    private readonly Dictionary<string, InfoCarrierRegistrationIdentity> _names = new(names, StringComparer.Ordinal);

    internal InfoCarrierServerRegistration Default => defaultIdentity is { } identity
        ? Get(identity)
        : throw new InvalidOperationException(
            "No default InfoCarrier server is registered. Call AddInfoCarrierServer without a name or select a server explicitly.");

    internal InfoCarrierServerRegistration Get(InfoCarrierRegistrationIdentity identity)
        => _registrations.TryGetValue(identity, out InfoCarrierServerRegistration? registration)
            ? registration
            : throw new InvalidOperationException("The InfoCarrier server handle does not belong to this host's registration snapshot.");

    internal InfoCarrierServerRegistration Get(string name)
        => _names.TryGetValue(name, out InfoCarrierRegistrationIdentity? identity)
            ? Get(identity)
            : throw new InvalidOperationException(
                $"No InfoCarrier server named '{name}' is registered. Call AddInfoCarrierServer with that name.");

    internal InfoCarrierServerRegistration GetForContext(Type contextType)
    {
        InfoCarrierServerRegistration[] matches = _registrations.Values.Where(r => r.ContextType == contextType).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"No InfoCarrier server is registered for context '{contextType.Name}'. Call AddInfoCarrierServer for that context."),
            _ => throw new InvalidOperationException(
                $"Multiple InfoCarrier servers are registered for context '{contextType.Name}'. Select a server name or handle."),
        };
    }

    internal InfoCarrierServerActivation ActivateDefault(IServiceProvider provider) => Activate(provider, Default.Identity);

    internal InfoCarrierServerActivation Activate(IServiceProvider provider, InfoCarrierRegistrationIdentity identity)
    {
        _ = Get(identity);
        return provider.GetRequiredKeyedService<InfoCarrierServerActivation>(identity);
    }
}

// Activations borrow the host-owned keyed components. Facades avoid duplicate disposal
// through interface aliases while preserving optional streaming capability.
internal sealed class InfoCarrierServerActivation
{
    internal InfoCarrierServerActivation(IServiceProvider provider, InfoCarrierServerRegistration registration)
    {
        Serializer = provider.GetRequiredKeyedService<IInfoCarrierSerializer>(registration.Identity);
        Server = provider.GetRequiredKeyedService<IInfoCarrierServer>(registration.Identity);
    }

    internal IInfoCarrierServer Server { get; }
    internal IInfoCarrierSerializer Serializer { get; }

}

internal sealed class DefaultInfoCarrierServer(IInfoCarrierServer server) : IInfoCarrierServer, IInfoCarrierStreamServer
{
    internal IInfoCarrierServer Server { get; } = server;

    public async IAsyncEnumerable<Query.QueryStreamFrame> QueryFramesAsync(QueryDataRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Server is IInfoCarrierStreamServer streaming)
        {
            await foreach (Query.QueryStreamFrame frame in streaming.QueryFramesAsync(request, cancellationToken)
                .ConfigureAwait(false)) { yield return frame; }
            yield break;
        }
        QueryDataResult result = await Server.QueryDataAsync(request, cancellationToken).ConfigureAwait(false);
        yield return new Query.QueryStartFrame(Query.QueryStreamFrameCodec.Version, 0, result.IsEntityResult, result.ElementTypeName);
        foreach (Expressions.DynamicValueNode row in BufferedQueryFrameAdapter.DeserializeRows(result.SerializedResults))
        { yield return new Query.QueryRowFrame(row); }
        foreach (ServerLogEvent log in result.ServerLog ?? []) { yield return new Query.QueryLogFrame(log); }
        yield return new Query.QueryCompleteFrame();
    }

    public QueryDataResult QueryData(QueryDataRequest request) => Server.QueryData(request);
    public SaveChangesResult SaveChanges(SaveChangesRequest request) => Server.SaveChanges(request);
    public TransactionResult BeginTransaction() => Server.BeginTransaction();
    public void CommitTransaction(string transactionId) => Server.CommitTransaction(transactionId);
    public void RollbackTransaction(string transactionId) => Server.RollbackTransaction(transactionId);
    public void CreateSavepoint(string transactionId, string name) => Server.CreateSavepoint(transactionId, name);
    public void RollbackToSavepoint(string transactionId, string name) => Server.RollbackToSavepoint(transactionId, name);
    public void ReleaseSavepoint(string transactionId, string name) => Server.ReleaseSavepoint(transactionId, name);
    public bool SupportsSavepoints(string transactionId) => Server.SupportsSavepoints(transactionId);
    public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
        => Server.QueryDataAsync(request, cancellationToken);
    public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default)
        => Server.SaveChangesAsync(request, cancellationToken);
    public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Server.BeginTransactionAsync(cancellationToken);
    public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => Server.CommitTransactionAsync(transactionId, cancellationToken);
    public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => Server.RollbackTransactionAsync(transactionId, cancellationToken);
    public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => Server.CreateSavepointAsync(transactionId, name, cancellationToken);
    public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => Server.RollbackToSavepointAsync(transactionId, name, cancellationToken);
    public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => Server.ReleaseSavepointAsync(transactionId, name, cancellationToken);
    public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
        => Server.SupportsSavepointsAsync(transactionId, cancellationToken);
}
