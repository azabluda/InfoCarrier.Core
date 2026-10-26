// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>Immutable construction settings for a selected client, available through inherited-key injection.</summary>
public sealed record InfoCarrierClientSettings(Uri? Endpoint, int? MaxResponseBytes, string MediaType, string Name);

/// <summary>A selected server and serializer for custom hosting adapters. The host owns both services.</summary>
public sealed class InfoCarrierServerEndpoint
{
    internal InfoCarrierServerEndpoint(InfoCarrierServerActivation activation, InfoCarrierServerRegistration registration)
    {
        Server = activation.Server;
        Serializer = activation.Serializer;
        MaxRequestBytes = registration.MaxRequestBytes;
        MediaType = registration.MediaType;
    }

    /// <summary>The complete selected server.</summary>
    public IInfoCarrierServer Server { get; }
    /// <summary>The selected serializer, including request-limit enforcement.</summary>
    public IInfoCarrierSerializer Serializer { get; }
    /// <summary>The request limit custom adapters must enforce before buffering.</summary>
    public int? MaxRequestBytes { get; }
    /// <summary>The configured envelope and payload media type. Stream frames remain JSON.</summary>
    public string MediaType { get; }
}

/// <summary>Resolves complete registrations for advanced clients and custom hosting adapters.</summary>
public static class InfoCarrierRegistrationAccess
{
    /// <summary>Resolves an unnamed or named client without exposing its internal service key.</summary>
    public static IInfoCarrierClient GetInfoCarrierClient(this IServiceProvider provider, string? clientName = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (clientName is null) { return provider.GetRequiredService<IInfoCarrierClient>(); }
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        return provider.GetRequiredService<InfoCarrierClientRegistrationStore>().Get(provider, clientName);
    }

    /// <summary>Resolves an unnamed or named server for custom hosting.</summary>
    public static InfoCarrierServerEndpoint GetInfoCarrierServerEndpoint(this IServiceProvider provider, string? serverName = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        InfoCarrierServerRegistrationStore store = provider.GetRequiredService<InfoCarrierServerRegistrationStore>();
        if (serverName is not null) { ArgumentException.ThrowIfNullOrWhiteSpace(serverName); }
        return Resolve(provider, store, serverName is null ? store.Default : store.Get(serverName));
    }

    /// <summary>Resolves a retained server handle; rejects handles outside this host's snapshot.</summary>
    public static InfoCarrierServerEndpoint GetInfoCarrierServerEndpoint(this IServiceProvider provider, InfoCarrierServerBuilder server)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(server);
        InfoCarrierServerRegistrationStore store = provider.GetRequiredService<InfoCarrierServerRegistrationStore>();
        return Resolve(provider, store, store.Get(server.Identity));
    }

    /// <summary>Resolves the sole registration for a context type; rejects ambiguity.</summary>
    public static InfoCarrierServerEndpoint GetInfoCarrierServerEndpoint<TContext>(this IServiceProvider provider) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(provider);
        InfoCarrierServerRegistrationStore store = provider.GetRequiredService<InfoCarrierServerRegistrationStore>();
        return Resolve(provider, store, store.GetForContext(typeof(TContext)));
    }

    private static InfoCarrierServerEndpoint Resolve(IServiceProvider provider, InfoCarrierServerRegistrationStore store,
        InfoCarrierServerRegistration registration)
        => new(store.Activate(provider, registration.Identity), registration);
}
