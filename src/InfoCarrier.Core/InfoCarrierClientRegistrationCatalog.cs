// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

internal sealed class InfoCarrierClientRegistrationCatalog
{
    private readonly Dictionary<string, InfoCarrierRegistrationIdentity> _names = new(StringComparer.Ordinal);
    private InfoCarrierRegistrationIdentity? _defaultIdentity;

    internal void ValidateName(string? name)
    {
        if (name is not null && _names.ContainsKey(name))
        {
            throw new InvalidOperationException($"An InfoCarrier client named '{name}' is already registered.");
        }
    }

    internal void Add(string? name, InfoCarrierClientRegistration registration)
    {
        ValidateName(name);
        if (name is null) { _defaultIdentity = registration.Identity; }
        else { _names.Add(name, registration.Identity); }
    }

    internal InfoCarrierClientRegistrationStore Snapshot() => new(_names, _defaultIdentity);
}

internal sealed class InfoCarrierClientRegistrationStore(
    IReadOnlyDictionary<string, InfoCarrierRegistrationIdentity> names,
    InfoCarrierRegistrationIdentity? defaultIdentity)
{
    private readonly Dictionary<string, InfoCarrierRegistrationIdentity> _names = new(names, StringComparer.Ordinal);

    internal IInfoCarrierClient GetDefault(IServiceProvider provider)
        => defaultIdentity is { } identity
            ? Resolve(provider, identity)
            : throw new InvalidOperationException(
                "No default InfoCarrier client is registered. Call AddInfoCarrierClient without a name.");

    internal IInfoCarrierClient Get(IServiceProvider provider, string name)
        => _names.TryGetValue(name, out InfoCarrierRegistrationIdentity? identity)
            ? Resolve(provider, identity)
            : throw new InvalidOperationException(
                $"No InfoCarrier client named '{name}' is registered. Call AddInfoCarrierClient with that name.");

    private static IInfoCarrierClient Resolve(IServiceProvider provider, InfoCarrierRegistrationIdentity identity)
        => provider.GetRequiredKeyedService<InfoCarrierClientActivation>(identity).Client;
}

// The host owns this complete activation and disposes the factory-created HttpClient wrapper.
// IHttpClientFactory retains ownership of pooled handlers. Direct creation retains caller ownership.
internal sealed class InfoCarrierClientActivation : IDisposable
{
    private readonly HttpClient _httpClient;

    internal InfoCarrierClientActivation(
        HttpClient httpClient, InfoCarrierClientRegistration registration, ILogger<TransportInfoCarrierClient> logger)
    {
        _httpClient = httpClient;
        try
        {
            Client = InfoCarrierRoleServiceCollectionExtensions.CreateClient(httpClient, registration, logger);
        }
        catch
        {
            httpClient.Dispose();
            throw;
        }
    }

    internal IInfoCarrierClient Client { get; }

    public void Dispose() => _httpClient.Dispose();
}
