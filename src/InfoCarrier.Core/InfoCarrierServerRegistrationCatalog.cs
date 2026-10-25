// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    internal void Add(string? name, InfoCarrierServerRegistration registration)
    {
        ValidateName(name);
        _registrations.Add(registration.Identity, registration);
        if (name is null) { _defaultIdentity = registration.Identity; }
        else { _names.Add(name, registration.Identity); }
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

// One host-owned activation owns the complete endpoint pair. Default resolution uses a non-owning
// facade so the container does not track and dispose the same server through two service aliases.
internal sealed class InfoCarrierServerActivation : IAsyncDisposable
{
    internal InfoCarrierServerActivation(IServiceProvider provider, InfoCarrierServerRegistration registration)
    {
        Serializer = new SystemTextJsonInfoCarrierSerializer(new InfoCarrierPayloadLimits(registration.MaxRequestBytes));
        Server = new InProcessInfoCarrierServer(provider,
            provider.GetRequiredService<ILogger<InProcessInfoCarrierServer>>(),
            provider.GetService<ServerLogCapture>(), registration);
    }

    internal InProcessInfoCarrierServer Server { get; }
    internal IInfoCarrierSerializer Serializer { get; }

    public ValueTask DisposeAsync() => Server.DisposeAsync();
}

internal sealed class DefaultInfoCarrierServer(InProcessInfoCarrierServer server) : IInfoCarrierServer
{
    internal InProcessInfoCarrierServer Server { get; } = server;

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
