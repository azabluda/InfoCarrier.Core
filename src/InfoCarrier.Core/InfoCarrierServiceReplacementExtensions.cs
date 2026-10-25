// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>Replaces registration-local InfoCarrier service implementations.</summary>
public static class InfoCarrierServiceReplacementExtensions
{
    /// <summary>Replaces a client serializer or transport, retaining its singleton lifetime.</summary>
    /// <remarks>
    /// The host constructs and owns the replacement. Constructor dependencies belonging to the
    /// same registration must use parameterless <see cref="FromKeyedServicesAttribute" />.
    /// Ordinary dependencies resolve from the host. Replacement is not service decoration.
    /// </remarks>
    public static InfoCarrierClientBuilder ReplaceService<TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        this IHttpClientBuilder builder)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder is not InfoCarrierClientBuilder client)
        {
            throw new InvalidOperationException("ReplaceService requires a builder returned by AddInfoCarrierClient.");
        }
        client.Replace<TService, TImplementation>();
        return client;
    }

    /// <summary>Replaces a server serializer, retaining its singleton lifetime.</summary>
    /// <remarks>The host owns the replacement. Other server service contracts are not yet supported.</remarks>
    public static InfoCarrierServerBuilder ReplaceService<TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        this InfoCarrierServerBuilder builder)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Replace<TService, TImplementation>();
        return builder;
    }

    /// <summary>Replaces the specified original client implementation, retaining its lifetime.</summary>
    /// <remarks>Specific original-implementation replacements take precedence over contract-wide replacements.</remarks>
    public static InfoCarrierClientBuilder ReplaceService<TService, TCurrentImplementation,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TNewImplementation>(
        this IHttpClientBuilder builder)
        where TService : class where TCurrentImplementation : class, TService where TNewImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder is not InfoCarrierClientBuilder client)
        {
            throw new InvalidOperationException("ReplaceService requires a builder returned by AddInfoCarrierClient.");
        }
        client.Replace<TService, TNewImplementation>(typeof(TCurrentImplementation));
        return client;
    }

    /// <summary>Replaces the specified original server implementation, retaining its lifetime.</summary>
    /// <remarks>Specific original-implementation replacements take precedence over contract-wide replacements.</remarks>
    public static InfoCarrierServerBuilder ReplaceService<TService, TCurrentImplementation,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TNewImplementation>(
        this InfoCarrierServerBuilder builder)
        where TService : class where TCurrentImplementation : class, TService where TNewImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Replace<TService, TNewImplementation>(typeof(TCurrentImplementation));
        return builder;
    }
}

/// <summary>Configures one InfoCarrier client and its private HTTP adapter.</summary>
/// <remarks>
/// Existing HTTP builder extensions remain available. A custom transport does not activate the
/// HTTP client or its handler configuration. The private HTTP name is not a public registration key.
/// </remarks>
public sealed class InfoCarrierClientBuilder : IHttpClientBuilder
{
    private InfoCarrierClientRegistration _registration;
    private readonly InfoCarrierClientRegistrationCatalog _catalog;

    internal InfoCarrierClientBuilder(IServiceCollection services, InfoCarrierClientRegistrationCatalog catalog,
        InfoCarrierClientRegistration registration)
    {
        Services = services;
        _catalog = catalog;
        _registration = registration;
    }

    /// <summary>The host service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>The private name used by the HTTP client factory.</summary>
    public string Name => _registration.HttpClientName;

    internal void Replace<TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        Type? currentImplementation = null)
        where TService : class where TImplementation : class, TService
    {
        IReadOnlyDictionary<(Type Contract, Type? Current), Type> replacements = InfoCarrierServiceComponents.Replace<TService, TImplementation>(
            Services, _registration.Identity, _registration.Replacements,
            new InfoCarrierPayloadLimits(maxResponseBytes: _registration.MaxResponseBytes), client: true, currentImplementation);
        _registration = _registration with { Replacements = replacements };
        _catalog.Replace(_registration);
    }
}

internal static class InfoCarrierServiceComponents
{
    internal static void AddSerializer(IServiceCollection services, InfoCarrierRegistrationIdentity identity,
        InfoCarrierPayloadLimits limits)
    {
        services.AddKeyedSingleton<SystemTextJsonInfoCarrierSerializer>(identity, (_, _) => new(limits));
        services.AddKeyedSingleton<IInfoCarrierSerializer>(identity, (provider, _) => new InfoCarrierSerializerBoundary(
            provider.GetRequiredKeyedService<SystemTextJsonInfoCarrierSerializer>(identity), limits));
    }

    internal static IReadOnlyDictionary<(Type Contract, Type? Current), Type> Replace<TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        IServiceCollection services, InfoCarrierRegistrationIdentity identity,
        IReadOnlyDictionary<(Type Contract, Type? Current), Type> previous, InfoCarrierPayloadLimits limits, bool client,
        Type? currentImplementation)
        where TService : class where TImplementation : class, TService
    {
        Type contract = typeof(TService);
        bool serializer = contract == typeof(IInfoCarrierSerializer);
        if (!serializer && !(client && contract == typeof(IInfoCarrierTransport)))
        {
            throw new InvalidOperationException($"InfoCarrier does not support replacement of '{contract.Name}' "
                + $"for this {(client ? "client" : "server")} registration.");
        }
        if (typeof(TImplementation).IsAbstract)
        {
            throw new ArgumentException($"Replacement '{typeof(TImplementation).Name}' must be a concrete class.");
        }
        Type original = serializer ? typeof(SystemTextJsonInfoCarrierSerializer) : typeof(HttpInfoCarrierTransport);
        var replacements = new Dictionary<(Type Contract, Type? Current), Type>(previous)
        {
            [(contract, currentImplementation)] = typeof(TImplementation),
        };
        Type previousType = Select(previous, contract, original);
        Type selected = Select(replacements, contract, original);
        if (selected == previousType) { return replacements; }
        Remove(services, contract, identity);
        if (serializer)
        {
            Remove(services, previousType, identity);
            services.AddKeyedSingleton<TImplementation>(identity);
            services.AddKeyedSingleton<IInfoCarrierSerializer>(identity, (provider, _) => new InfoCarrierSerializerBoundary(
                (IInfoCarrierSerializer)provider.GetRequiredKeyedService<TImplementation>(identity), limits));
        }
        else
        {
            services.AddKeyedSingleton<TService, TImplementation>(identity);
        }
        return replacements;

        static Type Select(IReadOnlyDictionary<(Type Contract, Type? Current), Type> choices, Type contract, Type original)
            => choices.GetValueOrDefault((contract, original)) ?? choices.GetValueOrDefault((contract, null)) ?? original;
    }

    private static void Remove(IServiceCollection services, Type contract, object identity)
    {
        foreach (ServiceDescriptor descriptor in services.Where(d => d.ServiceType == contract
            && d.IsKeyedService && ReferenceEquals(d.ServiceKey, identity)).ToArray())
        {
            services.Remove(descriptor);
        }
    }
}

// Non-owning policy boundary. The concrete serializer is separately constructed and disposed by
// the host. Limits apply before custom decoding, including serializers with no built-in limits.
internal sealed class InfoCarrierSerializerBoundary(IInfoCarrierSerializer inner, InfoCarrierPayloadLimits limits)
    : IInfoCarrierSerializer
{
    internal InfoCarrierPayloadLimits Limits { get; } = limits;

    public byte[] Serialize<T>(T value) => inner.Serialize(value);
    public ValueTask<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
        => inner.SerializeAsync(value, cancellationToken);
    public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default)
        => inner.SerializeAsync(destination, value, cancellationToken);

    public T? Deserialize<T>(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Limits.Guard<T>(payload.Length, $"payload for '{typeof(T).Name}'");
        return inner.Deserialize<T>(payload);
    }

    public ValueTask<T?> DeserializeAsync<T>(byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Limits.Guard<T>(payload.Length, $"payload for '{typeof(T).Name}'");
        return inner.DeserializeAsync<T>(payload, cancellationToken);
    }

    public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
    {
        using Stream? bounded = Limits.CreateReadLimit<T>(source);
        return await inner.DeserializeAsync<T>(bounded ?? source, cancellationToken).ConfigureAwait(false);
    }
}
