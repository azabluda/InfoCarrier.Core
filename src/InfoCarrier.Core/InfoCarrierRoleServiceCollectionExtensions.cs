// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>Registers complete InfoCarrier client and server roles.</summary>
public static class InfoCarrierRoleServiceCollectionExtensions
{
    /// <summary>Registers the default HTTP client role.</summary>
    public static IHttpClientBuilder AddInfoCarrierClient(
        this IServiceCollection services,
        Action<InfoCarrierClientOptions> configure)
        => AddClient(services, null, configure);

    /// <summary>Registers a named HTTP client without changing the unnamed default.</summary>
    /// <remarks>Names are case-sensitive. Duplicate names are rejected.</remarks>
    public static IHttpClientBuilder AddInfoCarrierClient(
        this IServiceCollection services,
        string clientName,
        Action<InfoCarrierClientOptions> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        return AddClient(services, clientName, configure);
    }

    private static IHttpClientBuilder AddClient(
        IServiceCollection services,
        string? clientName,
        Action<InfoCarrierClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        InfoCarrierClientRegistrationCatalog? catalog = services
            .Where(service => service.ServiceType == typeof(InfoCarrierClientRegistrationCatalog))
            .Select(service => service.ImplementationInstance)
            .OfType<InfoCarrierClientRegistrationCatalog>().SingleOrDefault();
        catalog?.ValidateName(clientName);
        var options = new InfoCarrierClientOptions();
        configure(options);
        InfoCarrierClientRegistration registration = options.Build();
        if (catalog is null)
        {
            catalog = new InfoCarrierClientRegistrationCatalog();
            services.AddSingleton(catalog);
            services.AddSingleton(provider =>
                provider.GetRequiredService<InfoCarrierClientRegistrationCatalog>().Snapshot());
        }
        catalog.Add(clientName, registration);

        // Key only the complete activation, never its serializer, mappers, or policy separately.
        services.AddKeyedSingleton<InfoCarrierClientActivation>(registration.Identity, (provider, _) =>
            new InfoCarrierClientActivation(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(registration.HttpClientName),
                registration, provider.GetRequiredService<ILogger<TransportInfoCarrierClient>>()));
        if (clientName is null)
        {
            services.RemoveAll<InfoCarrierClientRegistration>();
            services.AddSingleton(registration);
            services.RemoveAll<IInfoCarrierClient>();
            services.AddSingleton<IInfoCarrierClient>(provider => provider
                .GetRequiredService<InfoCarrierClientRegistrationStore>().GetDefault(provider));
        }

        return services.AddHttpClient(registration.HttpClientName);
    }

    /// <summary>Registers the default server role for <typeparamref name="TContext" />.</summary>
    public static InfoCarrierServerBuilder AddInfoCarrierServer<TContext>(
        this IServiceCollection services,
        Action<InfoCarrierServerOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new InfoCarrierServerOptions();
        configure?.Invoke(options);
        InfoCarrierServerRegistration registration = options.Build(typeof(TContext));

        InfoCarrierServerRegistrationCatalog registrations = services
            .Where(service => service.ServiceType == typeof(InfoCarrierServerRegistrationCatalog))
            .Select(service => service.ImplementationInstance)
            .OfType<InfoCarrierServerRegistrationCatalog>()
            .SingleOrDefault() ?? new InfoCarrierServerRegistrationCatalog();
        if (!services.Any(service => ReferenceEquals(service.ImplementationInstance, registrations)))
        {
            services.AddSingleton(registrations);
            services.AddSingleton(provider =>
                provider.GetRequiredService<InfoCarrierServerRegistrationCatalog>().Snapshot());
        }
        registrations.SetDefault(registration);
        services.RemoveAll<IInfoCarrierSerializer>();
        services.AddSingleton<IInfoCarrierSerializer>(provider =>
            new SystemTextJsonInfoCarrierSerializer(new InfoCarrierPayloadLimits(
                provider.GetRequiredService<InfoCarrierServerRegistrationStore>().Default.MaxRequestBytes)));
        services.RemoveAll<IInfoCarrierServer>();
        services.AddSingleton<IInfoCarrierServer>(provider => new InProcessInfoCarrierServer(
            provider,
            provider.GetRequiredService<ILogger<InProcessInfoCarrierServer>>(),
            provider.GetService<ServerLogCapture>(),
            provider.GetRequiredService<InfoCarrierServerRegistrationStore>().Default));

        if (registration.LogForwardingMinimumLevel is not null)
        {
            services.TryAddSingleton<ServerLogCapture>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, ServerLogCaptureProvider>());
        }

        return new InfoCarrierServerBuilder(services, registrations, registration);
    }

    internal static IInfoCarrierClient CreateClient(
        HttpClient httpClient,
        InfoCarrierClientRegistration registration,
        ILogger<TransportInfoCarrierClient> logger)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(
            new InfoCarrierPayloadLimits(maxResponseBytes: registration.MaxResponseBytes));
        var transport = new HttpInfoCarrierTransport(httpClient, serializer, registration.Endpoint!.ToString());
        return new TransportInfoCarrierClient(transport, serializer, logger, registration);
    }
}

/// <summary>Additional registrations for the default InfoCarrier server role.</summary>
public sealed class InfoCarrierServerBuilder
{
    private InfoCarrierServerRegistration _registration;
    private readonly InfoCarrierServerRegistrationCatalog _registrations;

    internal InfoCarrierServerBuilder(
        IServiceCollection services,
        InfoCarrierServerRegistrationCatalog registrations,
        InfoCarrierServerRegistration registration)
    {
        Services = services;
        _registrations = registrations;
        _registration = registration;
    }

    internal InfoCarrierRegistrationIdentity Identity => _registration.Identity;

    internal void ConfigureCallerIdentity(
        Func<IServiceProvider, IInfoCarrierServerCallerIdentity> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        InfoCarrierServerRegistration replacement = _registration with
        {
            CallerIdentityFactory = factory,
        };
        _registrations.Replace(replacement);
        _registration = replacement;
    }

    /// <summary>The application's service collection.</summary>
    public IServiceCollection Services { get; }
}

internal sealed class InfoCarrierServerRegistrationCatalog
{
    private readonly Dictionary<InfoCarrierRegistrationIdentity, InfoCarrierServerRegistration> _registrations = [];
    private InfoCarrierRegistrationIdentity? _defaultIdentity;

    internal void SetDefault(InfoCarrierServerRegistration registration)
    {
        _registrations.Add(registration.Identity, registration);
        _defaultIdentity = registration.Identity;
    }

    internal void Replace(InfoCarrierServerRegistration registration)
        => _registrations[registration.Identity] = registration;

    internal InfoCarrierServerRegistrationStore Snapshot()
        => _defaultIdentity is { } defaultIdentity
            ? new InfoCarrierServerRegistrationStore(_registrations, defaultIdentity)
            : throw new InvalidOperationException("No default InfoCarrier server is registered.");
}

internal sealed class InfoCarrierServerRegistrationStore(
    IReadOnlyDictionary<InfoCarrierRegistrationIdentity, InfoCarrierServerRegistration> registrations,
    InfoCarrierRegistrationIdentity defaultIdentity)
{
    private readonly IReadOnlyDictionary<InfoCarrierRegistrationIdentity, InfoCarrierServerRegistration> _registrations
        = new Dictionary<InfoCarrierRegistrationIdentity, InfoCarrierServerRegistration>(registrations);

    internal InfoCarrierServerRegistration Default => _registrations[defaultIdentity];

    internal InfoCarrierServerRegistration Get(InfoCarrierRegistrationIdentity identity)
        => _registrations[identity];
}

/// <summary>Creates an HTTP InfoCarrier client without dependency injection.</summary>
public static class InfoCarrierClient
{
    /// <summary>Creates a client. The caller owns <paramref name="httpClient" />.</summary>
    public static IInfoCarrierClient Create(
        HttpClient httpClient,
        Action<InfoCarrierClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new InfoCarrierClientOptions();
        configure(options);
        InfoCarrierClientRegistration registration = options.Build();
        return InfoCarrierRoleServiceCollectionExtensions.CreateClient(
            httpClient,
            registration,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TransportInfoCarrierClient>.Instance);
    }

    /// <summary>Applies client role options to an existing transport-independent client.</summary>
    public static IInfoCarrierClient Configure(
        IInfoCarrierClient client,
        Action<InfoCarrierClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new InfoCarrierClientOptions();
        configure(options);
        return new ConfiguredInfoCarrierClient(client, options.Build(requireEndpoint: false));
    }

    private sealed class ConfiguredInfoCarrierClient(
        IInfoCarrierClient inner,
        InfoCarrierClientRegistration registration)
        : IInfoCarrierClient, IInfoCarrierClientConfiguration
    {
        public InfoCarrierClientRegistration? Registration { get; set; } = registration;

        public QueryDataResult QueryData(QueryDataRequest request, DbContext clientContext)
            => inner.QueryData(request, clientContext);
        public SaveChangesResult SaveChanges(SaveChangesRequest request, DbContext clientContext)
            => inner.SaveChanges(request, clientContext);
        public TransactionResult BeginTransaction() => inner.BeginTransaction();
        public void CommitTransaction(string transactionId) => inner.CommitTransaction(transactionId);
        public void RollbackTransaction(string transactionId) => inner.RollbackTransaction(transactionId);
        public void CreateSavepoint(string transactionId, string name) => inner.CreateSavepoint(transactionId, name);
        public void RollbackToSavepoint(string transactionId, string name) => inner.RollbackToSavepoint(transactionId, name);
        public void ReleaseSavepoint(string transactionId, string name) => inner.ReleaseSavepoint(transactionId, name);
        public bool SupportsSavepoints(string transactionId) => inner.SupportsSavepoints(transactionId);
        public IAsyncEnumerable<QueryStreamFrame> QueryDataStreamAsync(
            QueryDataRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
            => inner.QueryDataStreamAsync(request, clientContext, cancellationToken);
        public Task<QueryDataResult> QueryDataAsync(
            QueryDataRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
            => inner.QueryDataAsync(request, clientContext, cancellationToken);
        public Task<SaveChangesResult> SaveChangesAsync(
            SaveChangesRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
            => inner.SaveChangesAsync(request, clientContext, cancellationToken);
        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.CommitTransactionAsync(transactionId, cancellationToken);
        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.RollbackTransactionAsync(transactionId, cancellationToken);
        public Task CreateSavepointAsync(
            string transactionId, string name, CancellationToken cancellationToken = default)
            => inner.CreateSavepointAsync(transactionId, name, cancellationToken);
        public Task RollbackToSavepointAsync(
            string transactionId, string name, CancellationToken cancellationToken = default)
            => inner.RollbackToSavepointAsync(transactionId, name, cancellationToken);
        public Task ReleaseSavepointAsync(
            string transactionId, string name, CancellationToken cancellationToken = default)
            => inner.ReleaseSavepointAsync(transactionId, name, cancellationToken);
        public Task<bool> SupportsSavepointsAsync(
            string transactionId, CancellationToken cancellationToken = default)
            => inner.SupportsSavepointsAsync(transactionId, cancellationToken);
    }
}

internal interface IInfoCarrierClientConfiguration
{
    InfoCarrierClientRegistration? Registration { get; set; }
}
