// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

internal sealed class InfoCarrierClientRegistrationCatalog
{
    private readonly Dictionary<string, InfoCarrierRegistrationIdentity> _names = new(StringComparer.Ordinal);
    private readonly Dictionary<InfoCarrierRegistrationIdentity, InfoCarrierClientRegistration> _registrations = [];
    private InfoCarrierRegistrationIdentity? _defaultIdentity;
    private int _registrationCount;

    internal void ValidateName(string? name)
    {
        if (name is not null && _names.ContainsKey(name))
        {
            throw new InvalidOperationException($"An InfoCarrier client named '{name}' is already registered.");
        }
    }

    internal InfoCarrierClientRegistration Add(string? name, InfoCarrierClientRegistration registration)
    {
        ValidateName(name);
        registration = registration with { MetricIdentity = new(name ?? "", ++_registrationCount) };
        _registrations.Add(registration.Identity, registration);
        if (name is null) { _defaultIdentity = registration.Identity; }
        else { _names.Add(name, registration.Identity); }
        return registration;
    }

    internal void Replace(InfoCarrierClientRegistration registration) => _registrations[registration.Identity] = registration;

    internal InfoCarrierClientRegistrationStore Snapshot() => new(_registrations, _names, _defaultIdentity);
}

internal sealed class InfoCarrierClientRegistrationStore(
    IReadOnlyDictionary<InfoCarrierRegistrationIdentity, InfoCarrierClientRegistration> registrations,
    IReadOnlyDictionary<string, InfoCarrierRegistrationIdentity> names,
    InfoCarrierRegistrationIdentity? defaultIdentity)
{
    private readonly Dictionary<InfoCarrierRegistrationIdentity, InfoCarrierClientRegistration> _registrations = new(registrations);
    private readonly Dictionary<string, InfoCarrierRegistrationIdentity> _names = new(names, StringComparer.Ordinal);

    internal InfoCarrierClientRegistration Registration(InfoCarrierRegistrationIdentity identity) => _registrations[identity];

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

// The activation borrows keyed dependencies. The host owns their disposal, including HTTP wrappers;
// IHttpClientFactory retains ownership of pooled handlers. Direct creation retains caller ownership.
internal sealed class InfoCarrierClientActivation
{
    internal InfoCarrierClientActivation(
        IServiceProvider provider, InfoCarrierClientRegistration registration)
    {
        Client = new TransportInfoCarrierClient(
            provider.GetRequiredKeyedService<IInfoCarrierTransport>(registration.Identity),
            provider.GetRequiredKeyedService<IInfoCarrierSerializer>(registration.Identity),
            provider.GetRequiredService<ILogger<TransportInfoCarrierClient>>(), registration);
    }

    internal IInfoCarrierClient Client { get; }

}
