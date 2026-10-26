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
    public static InfoCarrierClientBuilder AddInfoCarrierClient(
        this IServiceCollection services,
        Action<InfoCarrierClientOptions> configure)
        => AddClient(services, null, configure);

    /// <summary>Registers a named HTTP client without changing the unnamed default.</summary>
    /// <remarks>Names are case-sensitive. Duplicate names are rejected.</remarks>
    public static InfoCarrierClientBuilder AddInfoCarrierClient(
        this IServiceCollection services,
        string clientName,
        Action<InfoCarrierClientOptions> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        return AddClient(services, clientName, configure);
    }

    private static InfoCarrierClientBuilder AddClient(
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
        InfoCarrierClientRegistration registration = options.Build(requireEndpoint: false);
        if (catalog is null)
        {
            catalog = new InfoCarrierClientRegistrationCatalog();
            services.AddSingleton(catalog);
            services.AddSingleton(provider =>
                provider.GetRequiredService<InfoCarrierClientRegistrationCatalog>().Snapshot());
        }
        registration = catalog.Add(clientName, registration);

        InfoCarrierServiceComponents.AddSerializer(services, registration.Identity,
            new InfoCarrierPayloadLimits(maxResponseBytes: registration.MaxResponseBytes));
        services.AddKeyedSingleton<HttpClient>(registration.Identity, (provider, _) =>
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(registration.HttpClientName));
        services.AddKeyedSingleton<IInfoCarrierTransport>(registration.Identity, (provider, _) =>
            new HttpInfoCarrierTransport(provider.GetRequiredKeyedService<HttpClient>(registration.Identity),
                provider.GetRequiredKeyedService<IInfoCarrierSerializer>(registration.Identity), (registration.Endpoint ?? throw new InvalidOperationException(
                    "The HTTP transport requires an absolute Endpoint. Configure WithEndpoint or replace IInfoCarrierTransport." )).ToString())
            { MediaType = registration.MediaType });
        services.AddKeyedSingleton<TransportInfoCarrierClient>(registration.Identity, (provider, _) =>
            new TransportInfoCarrierClient(
                provider.GetRequiredKeyedService<IInfoCarrierTransport>(registration.Identity),
                provider.GetRequiredKeyedService<IInfoCarrierSerializer>(registration.Identity),
                provider.GetRequiredService<ILogger<TransportInfoCarrierClient>>(), provider
                    .GetRequiredService<InfoCarrierClientRegistrationStore>().Registration(registration.Identity), measureRequests: false));
        services.AddKeyedSingleton<IInfoCarrierClient>(registration.Identity, (provider, _) =>
            provider.GetRequiredKeyedService<TransportInfoCarrierClient>(registration.Identity));
        services.AddKeyedSingleton<InfoCarrierClientSettings>(registration.Identity, (provider, _) =>
        {
            InfoCarrierClientRegistration selected = provider.GetRequiredService<InfoCarrierClientRegistrationStore>()
                .Registration(registration.Identity);
            return new(selected.Endpoint, selected.MaxResponseBytes, selected.MediaType, selected.MetricIdentity!.Name);
        });
        // Policy stays in the complete descriptor. Keyed component construction uses the same
        // identity, so the host owns replacements and their inherited-key dependencies.
        services.AddKeyedSingleton<InfoCarrierClientActivation>(registration.Identity, (provider, _) =>
            new InfoCarrierClientActivation(provider, provider
                .GetRequiredService<InfoCarrierClientRegistrationStore>().Registration(registration.Identity)));
        if (clientName is null)
        {
            RemoveUnkeyed<IInfoCarrierClient>(services);
            services.AddSingleton<IInfoCarrierClient>(provider => provider
                .GetRequiredService<InfoCarrierClientRegistrationStore>().GetDefault(provider));
        }

        services.AddHttpClient(registration.HttpClientName);
        return new InfoCarrierClientBuilder(services, catalog, registration);
    }

    /// <summary>Registers the default server role for <typeparamref name="TContext" />.</summary>
    public static InfoCarrierServerBuilder AddInfoCarrierServer<TContext>(
        this IServiceCollection services,
        Action<InfoCarrierServerOptions>? configure = null)
        where TContext : DbContext
        => AddServer<TContext>(services, null, configure, null);

    /// <summary>Registers a named server without changing the unnamed default.</summary>
    public static InfoCarrierServerBuilder AddInfoCarrierServer<TContext>(
        this IServiceCollection services,
        string serverName,
        Action<InfoCarrierServerOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        return AddServer<TContext>(services, serverName, configure, null);
    }

    /// <summary>Registers a named server with an application-selected, host-owned scoped context.</summary>
    /// <remarks>
    /// The resolver receives the operation or transaction scope. Resolve a context owned by that
    /// scope, such as a keyed scoped registration. InfoCarrier does not separately dispose the context.
    /// </remarks>
    public static InfoCarrierServerBuilder AddInfoCarrierServer<TContext>(
        this IServiceCollection services,
        string serverName,
        Action<InfoCarrierServerOptions>? configure,
        Func<IServiceProvider, TContext> resolveContext)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentNullException.ThrowIfNull(resolveContext);
        return AddServer(services, serverName, configure, resolveContext);
    }

    private static InfoCarrierServerBuilder AddServer<TContext>(
        IServiceCollection services,
        string? serverName,
        Action<InfoCarrierServerOptions>? configure,
        Func<IServiceProvider, TContext>? resolveContext)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        InfoCarrierServerRegistrationCatalog? existing = services
            .Where(service => service.ServiceType == typeof(InfoCarrierServerRegistrationCatalog))
            .Select(service => service.ImplementationInstance)
            .OfType<InfoCarrierServerRegistrationCatalog>().SingleOrDefault();
        existing?.ValidateName(serverName);
        var options = new InfoCarrierServerOptions();
        configure?.Invoke(options);
        InfoCarrierServerRegistration registration = options.Build(typeof(TContext)) with
        {
            ContextResolver = resolveContext is null ? null : provider => resolveContext(provider),
        };

        InfoCarrierServerRegistrationCatalog registrations = existing ?? new InfoCarrierServerRegistrationCatalog();
        if (existing is null)
        {
            services.AddSingleton(registrations);
            services.AddSingleton(provider =>
                provider.GetRequiredService<InfoCarrierServerRegistrationCatalog>().Snapshot());
        }
        registration = registrations.Add(serverName, registration);
        InfoCarrierServiceComponents.AddSerializer(services, registration.Identity,
            new InfoCarrierPayloadLimits(registration.MaxRequestBytes));
        services.AddKeyedSingleton<IInfoCarrierServerPipelineFactory, InfoCarrierServerPipelineFactory>(registration.Identity);
        services.AddKeyedSingleton<InProcessInfoCarrierServer>(registration.Identity, (provider, _) =>
            new InProcessInfoCarrierServer(provider, provider.GetRequiredService<ILogger<InProcessInfoCarrierServer>>(),
                provider.GetService<ServerLogCapture>(), provider
                    .GetRequiredService<InfoCarrierServerRegistrationStore>().Get(registration.Identity)));
        services.AddKeyedSingleton<IInfoCarrierServer>(registration.Identity, (provider, _) =>
            new DefaultInfoCarrierServer(provider.GetRequiredKeyedService<InProcessInfoCarrierServer>(registration.Identity)));
        services.AddKeyedSingleton<InfoCarrierServerActivation>(registration.Identity, (provider, _) =>
            new InfoCarrierServerActivation(provider, provider
                .GetRequiredService<InfoCarrierServerRegistrationStore>().Get(registration.Identity)));
        if (serverName is null)
        {
            foreach (ServiceDescriptor descriptor in services.Where(d => d.ServiceType == typeof(IInfoCarrierSerializer)
                && !d.IsKeyedService).ToArray()) { services.Remove(descriptor); }
            services.AddSingleton<IInfoCarrierSerializer>(provider => provider
                .GetRequiredService<InfoCarrierServerRegistrationStore>().ActivateDefault(provider).Serializer);
            RemoveUnkeyed<IInfoCarrierServer>(services);
            services.AddSingleton<IInfoCarrierServer>(provider => new DefaultInfoCarrierServer(provider
                .GetRequiredService<InfoCarrierServerRegistrationStore>().ActivateDefault(provider).Server));
        }

        if (registration.LogForwardingMinimumLevel is not null)
        {
            services.TryAddSingleton<ServerLogCapture>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, ServerLogCaptureProvider>());
        }

        return new InfoCarrierServerBuilder(services, registrations, registration);
    }

    private static void RemoveUnkeyed<TService>(IServiceCollection services)
    {
        foreach (ServiceDescriptor descriptor in services.Where(d => d.ServiceType == typeof(TService)
            && !d.IsKeyedService).ToArray()) { services.Remove(descriptor); }
    }

    internal static IInfoCarrierClient CreateClient(
        HttpClient httpClient,
        InfoCarrierClientRegistration registration,
        ILogger<TransportInfoCarrierClient> logger)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(
            new InfoCarrierPayloadLimits(maxResponseBytes: registration.MaxResponseBytes));
        var transport = new HttpInfoCarrierTransport(httpClient, serializer, registration.Endpoint!.ToString()) { MediaType = registration.MediaType };
        return new TransportInfoCarrierClient(transport, serializer, logger, registration);
    }
}

/// <summary>Additional configuration and an endpoint handle for one InfoCarrier server registration.</summary>
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

    internal void Replace<TService,
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        Type? currentImplementation = null)
        where TService : class where TImplementation : class, TService
    {
        IReadOnlyDictionary<(Type Contract, Type? Current), Type> replacements = InfoCarrierServiceComponents.Replace<TService, TImplementation>(
            Services, Identity, _registration.Replacements,
            new InfoCarrierPayloadLimits(_registration.MaxRequestBytes), client: false, currentImplementation);
        _registration = _registration with { Replacements = replacements };
        _registrations.Replace(_registration);
    }

    /// <summary>The application's service collection.</summary>
    public IServiceCollection Services { get; }
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

    internal static IInfoCarrierClient Configure(IInfoCarrierClient client, InfoCarrierClientRegistration registration)
        => new ConfiguredInfoCarrierClient(client, registration, measureRequests: true);

    private sealed class ConfiguredInfoCarrierClient(
        IInfoCarrierClient inner,
        InfoCarrierClientRegistration registration, bool measureRequests = false)
        : IInfoCarrierClient, IInfoCarrierClientConfiguration
    {
        public InfoCarrierClientRegistration? Registration { get; set; } = registration;

        public QueryDataResult QueryData(QueryDataRequest request, DbContext clientContext)
            => Measure(InfoCarrierOperation.Query, () => inner.QueryData(request, clientContext));
        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
            => MeasureAsync(InfoCarrierOperation.Query, () => inner.QueryDataAsync(request, clientContext, cancellationToken), cancellationToken);
        public SaveChangesResult SaveChanges(SaveChangesRequest request, DbContext clientContext)
            => Measure(InfoCarrierOperation.SaveChanges, () => inner.SaveChanges(request, clientContext));
        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
            => MeasureAsync(InfoCarrierOperation.SaveChanges, () => inner.SaveChangesAsync(request, clientContext, cancellationToken), cancellationToken);
        public TransactionResult BeginTransaction()
            => Measure(InfoCarrierOperation.BeginTransaction, () => inner.BeginTransaction());
        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => MeasureAsync(InfoCarrierOperation.BeginTransaction, () => inner.BeginTransactionAsync(cancellationToken), cancellationToken);
        public void CommitTransaction(string transactionId)
            => Measure<object?>(InfoCarrierOperation.CommitTransaction, () => { inner.CommitTransaction(transactionId); return null; });
        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => MeasureAsync<object?>(InfoCarrierOperation.CommitTransaction, async () => { await inner.CommitTransactionAsync(transactionId, cancellationToken).ConfigureAwait(false); return null; }, cancellationToken);
        public void RollbackTransaction(string transactionId)
            => Measure<object?>(InfoCarrierOperation.RollbackTransaction, () => { inner.RollbackTransaction(transactionId); return null; });
        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => MeasureAsync<object?>(InfoCarrierOperation.RollbackTransaction, async () => { await inner.RollbackTransactionAsync(transactionId, cancellationToken).ConfigureAwait(false); return null; }, cancellationToken);
        public void CreateSavepoint(string transactionId, string name)
            => Measure<object?>(InfoCarrierOperation.CreateSavepoint, () => { inner.CreateSavepoint(transactionId, name); return null; });
        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => MeasureAsync<object?>(InfoCarrierOperation.CreateSavepoint, async () => { await inner.CreateSavepointAsync(transactionId, name, cancellationToken).ConfigureAwait(false); return null; }, cancellationToken);
        public void RollbackToSavepoint(string transactionId, string name)
            => Measure<object?>(InfoCarrierOperation.RollbackToSavepoint, () => { inner.RollbackToSavepoint(transactionId, name); return null; });
        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => MeasureAsync<object?>(InfoCarrierOperation.RollbackToSavepoint, async () => { await inner.RollbackToSavepointAsync(transactionId, name, cancellationToken).ConfigureAwait(false); return null; }, cancellationToken);
        public void ReleaseSavepoint(string transactionId, string name)
            => Measure<object?>(InfoCarrierOperation.ReleaseSavepoint, () => { inner.ReleaseSavepoint(transactionId, name); return null; });
        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => MeasureAsync<object?>(InfoCarrierOperation.ReleaseSavepoint, async () => { await inner.ReleaseSavepointAsync(transactionId, name, cancellationToken).ConfigureAwait(false); return null; }, cancellationToken);
        public bool SupportsSavepoints(string transactionId)
            => Measure(InfoCarrierOperation.SupportsSavepoints, () => inner.SupportsSavepoints(transactionId));
        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
            => MeasureAsync(InfoCarrierOperation.SupportsSavepoints, () => inner.SupportsSavepointsAsync(transactionId, cancellationToken), cancellationToken);

        private T Measure<T>(InfoCarrierOperation operation, Func<T> execute)
        {
            using InfoCarrierRequestMeasurement? measurement = measureRequests ? InfoCarrierMetrics.StartClient(Registration?.MetricIdentity, operation) : null;
            try { T result = execute(); measurement?.Complete(); return result; }
            catch (Exception exception) { measurement?.Fail(exception, default); throw; }
        }

        private async Task<T> MeasureAsync<T>(InfoCarrierOperation operation, Func<Task<T>> execute, CancellationToken token)
        {
            using InfoCarrierRequestMeasurement? measurement = measureRequests ? InfoCarrierMetrics.StartClient(Registration?.MetricIdentity, operation) : null;
            try { T result = await execute().ConfigureAwait(false); measurement?.Complete(); return result; }
            catch (Exception exception) { measurement?.Fail(exception, token); throw; }
        }

        public async IAsyncEnumerable<QueryStreamFrame> QueryDataStreamAsync(QueryDataRequest request, DbContext clientContext,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using InfoCarrierRequestMeasurement? measurement = measureRequests ? InfoCarrierMetrics.StartClient(Registration?.MetricIdentity, InfoCarrierOperation.Query) : null;
            IAsyncEnumerator<QueryStreamFrame>? frames = null;
            try
            {
                try { frames = inner.QueryDataStreamAsync(request, clientContext, cancellationToken).GetAsyncEnumerator(cancellationToken); }
                catch (Exception exception) { measurement?.Fail(exception, cancellationToken); throw; }
                while (true)
                {
                    bool next;
                    try { next = await frames.MoveNextAsync().ConfigureAwait(false); }
                    catch (Exception exception) { measurement?.Fail(exception, cancellationToken); throw; }
                    if (!next) { yield break; }
                    QueryStreamFrame frame = frames.Current;
                    if (frame is QueryFaultFrame fault) { measurement?.FailFault(fault.Fault); }
                    if (frame is QueryCompleteFrame) { measurement?.Complete(); }
                    yield return frame;
                }
            }
            finally
            {
                if (frames is not null)
                {
                    try { await frames.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception exception) { measurement?.Fail(exception, cancellationToken); throw; }
                }
            }
        }

    }
}

internal interface IInfoCarrierClientConfiguration
{
    InfoCarrierClientRegistration? Registration { get; set; }
}
