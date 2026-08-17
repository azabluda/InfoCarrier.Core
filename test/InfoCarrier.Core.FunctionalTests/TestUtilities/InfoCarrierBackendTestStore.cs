// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
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

        IServiceCollection services = AddServices(new ServiceCollection().AddLogging());
        if (testStoreProperties.OnAddServices is { } onAddServices)
        {
            services = onAddServices(services);
        }

        services = services
            .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
            .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()

            // The server half of ADR-012's seam. A value the wire cannot walk has to be mapped
            // on *both* halves — the client's is registered in
            // `InfoCarrierTestStoreFactory.AddProviderServices` — and a mapper registered on one
            // side only fails asymmetrically, which is precisely the "computed twice by two
            // providers" hazard the ADR states the contract in CLR-type terms to avoid.
            .AddSingleton<InfoCarrier.Core.ValueMapping.IInfoCarrierValueMapper, InfoCarrierNetTopologySuiteValueMapper>()

            // The server half of ADR-012's standard mappers. The client gets them from
            // `AddEntityFrameworkInfoCarrier`; a server builds its own collection, so it asks.
            .AddInfoCarrierStandardValueMappers();

        // Only when the fixture has one. A `NonSharedModelTestBase` context usually declares its
        // whole model in its own `OnModelCreating`, and EF's own base registers a `TestModelSource`
        // only when the test supplies a customization — registering one built from `null` would
        // replace the context's model with an empty one.
        if (_testStoreProperties.OnModelCreating is { } modelCustomization)
        {
            services = services.AddSingleton(
                TestModelSource.GetFactory(modelCustomization, _testStoreProperties.ConfigureConventions));
        }

        services = services
            .AddDbContext(
                ServerContextType,
                (s, b) => AddProviderOptions(b),
                ServiceLifetime.Transient,
                ServiceLifetime.Singleton);

        // Only when the fixture's context is a *subclass*. A spec fixture may be
        // `SharedStoreFixtureBase<DbContext>` — `LazyLoadProxyTestBase`'s is — and then this
        // registration would re-register `DbContext` itself as scoped, overriding the transient
        // one `AddDbContext` just made and making it unresolvable from the root provider:
        // "cannot resolve scoped service 'DbContext' from root provider", every test in the
        // class failing identically before any of them ran.
        if (ServerContextType != typeof(DbContext))
        {
            SharedTestStoreProperties props = _testStoreProperties;
            services = services.AddScoped<DbContext>(sp =>
            {
                var serverContext = (DbContext)sp.GetRequiredService(ServerContextType);

                // The one place a request's client context and its server context are both in
                // hand. `CopyDbContextParameters` had been declared and assigned since the
                // factory was written and never invoked, which is why three query-filter tests
                // read the server's default tenant instead of the client's.
                if (CurrentClientContext.Value is { } clientContext)
                {
                    props.CopyDbContextParameters?.Invoke(clientContext, serverContext);
                }

                return serverContext;
            });
        }

        ServiceProvider = services.BuildServiceProvider(validateScopes: true);

        _serializer = ServiceProvider.GetRequiredService<IInfoCarrierSerializer>();

        // The whole suite now goes through the real envelope path (C45): the product's
        // `TransportInfoCarrierClient` wraps each request in an `InfoCarrierEnvelope`, a transport
        // carries it, and the product's `InfoCarrierEnvelopeServer` checks the protocol version
        // and dispatches. Before this the store implemented `IInfoCarrierClient` itself and both
        // halves of the envelope protocol were unexercised — the M5 exit criterion.
        _client = new TransportInfoCarrierClient(
            new EnvelopeTransport(
                (envelope, cancellationToken) => new InfoCarrierEnvelopeServer(CreateServer(), _serializer)
                    .DispatchAsync(envelope, cancellationToken),
                (envelope, cancellationToken) => new InfoCarrierEnvelopeServer(CreateServer(), _serializer)
                    .DispatchQueryAsync(envelope, cancellationToken)),
            _serializer);
    }

    private readonly IInfoCarrierClient _client;

    /// <summary>
    ///     Carries an envelope to the in-process server.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Not <see cref="InProcessInfoCarrierTransport" />, and the reason is a
    ///         measurement rather than a preference.</b> That transport re-serializes the whole
    ///         envelope, and an envelope's payload is <em>already serialized bytes</em> — so the
    ///         payload would be base64'd into a second JSON document on every hop. C37 measured
    ///         this suite's largest result at <b>560,839,164 bytes</b>; base64 makes that about
    ///         750 MB of additional JSON, twice per query, for coverage that is already had.
    ///     </para>
    ///     <para>
    ///         <b>Nothing is lost by not doing it.</b> The payload makes a genuine round trip
    ///         regardless — <see cref="TransportInfoCarrierClient" /> serializes it and
    ///         <see cref="InfoCarrierEnvelopeServer" /> deserializes it — which is where every
    ///         wire-serializability failure this suite has ever caught was caught. What this
    ///         transport adds is the envelope itself: the version is checked, the operation is
    ///         dispatched by discriminator, and the response comes back wrapped.
    ///     </para>
    ///     <para>
    ///         The envelope's own serializability is covered by <c>InMemorySmokeTest</c>, which
    ///         uses the real <see cref="InProcessInfoCarrierTransport" /> on small payloads.
    ///     </para>
    /// </remarks>
    /// <remarks>
    ///     <para>
    ///         The handler takes the <see cref="CancellationToken" /> as well as the envelope, and
    ///         that is not decoration (C66). It used to be a
    ///         <c>Func&lt;InfoCarrierEnvelope, Task&lt;InfoCarrierEnvelope&gt;&gt;</c>, so this
    ///         transport <b>dropped the caller's token on the floor</b> and every server operation
    ///         in the whole suite ran with <see cref="CancellationToken.None" /> — thousands of
    ///         `…Async(cancellationToken)` calls whose token stopped here.
    ///     </para>
    ///     <para>
    ///         The product was never wrong: <see cref="InProcessInfoCarrierTransport" /> has always
    ///         taken the two-argument handler. This is the same shape as C45's finding — a wire
    ///         concern that only the harness stood between the suite and — and it is why W6 could
    ///         be threaded end to end and still be untested.
    ///     </para>
    /// </remarks>
    private sealed class EnvelopeTransport(
        Func<InfoCarrierEnvelope, CancellationToken, Task<InfoCarrierEnvelope>> handler,
        Func<InfoCarrierEnvelope, CancellationToken, IAsyncEnumerable<QueryStreamItem>> queryHandler)
        : IInfoCarrierTransport
    {
        public Task<InfoCarrierEnvelope> SendAsync(
            InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => handler(request, cancellationToken);

        /// <summary>
        ///     Carries a streamed query response, serializing every item on the way past.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <b>The serialization is the point, and it is newly this class's job.</b> This
        ///         transport hands the envelope straight to the server because
        ///         <see cref="InfoCarrierEnvelopeServer" /> used to serialize the payload itself —
        ///         so every result row in the whole suite crossed real JSON without this class
        ///         doing anything. D7 half (A) moved the query response out of the envelope, and
        ///         with it that free coverage: without the round trip below, 22 656 tests would run
        ///         against live <c>DynamicValueNode</c> objects and stop proving the result wire
        ///         format entirely.
        ///     </para>
        ///     <para>
        ///         Per item, not per response — a simulation that buffered the items in order to
        ///         serialize them together would be testing the opposite of what streaming is.
        ///     </para>
        /// </remarks>
        public Task<QueryDataResult> SendQueryAsync(
            InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => QueryStreamReader.ReadAsync(
                Simulate(queryHandler(request, cancellationToken), cancellationToken),
                "the in-process backend test store",
                owner: null,
                cancellationToken);

        private static async IAsyncEnumerable<QueryStreamItem> Simulate(
            IAsyncEnumerable<QueryStreamItem> items,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (QueryStreamItem item in
                items.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                // Through `ExpressionJsonContext`, which is the context the real bindings use for a
                // query response: a `DynamicValueNode` is only correct under its options.
                yield return System.Text.Json.JsonSerializer.Deserialize(
                    System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                        item, ExpressionJsonContext.Default.QueryStreamItem),
                    ExpressionJsonContext.Default.QueryStreamItem)!;
            }
        }
    }

    /// <summary>
    ///     The server URL/name this store stands in for.
    /// </summary>
    public string ServerUrl => Name;

    /// <summary>
    ///     Creates a server-side <see cref="DbContext" /> from the server provider.
    /// </summary>
    public virtual DbContext CreateDbContext()
        => (DbContext)ServiceProvider.GetRequiredService(ServerContextType);

    /// <summary>
    ///     The context type the server runs, which may add store-specific model configuration
    ///     the client neither has nor needs (e.g. defining queries for keyless entity types).
    /// </summary>
    private Type ServerContextType
        => _testStoreProperties.ServerContextType ?? _testStoreProperties.ContextType;

    /// <summary>
    ///     Adds the backend provider services (e.g. InMemory, SqlServer) to the collection.
    /// </summary>
    protected abstract IServiceCollection AddServices(IServiceCollection serviceCollection);

    /// <summary>
    ///     Builds the server context's options.
    /// </summary>
    /// <remarks>
    ///     <see cref="EnableSensitiveDataLogging" /> because the spec fixtures set it on the
    ///     client (<c>FixtureBase.AddOptions</c>) and an exception raised while the *server*
    ///     compiles the same query should read the same way. Without it,
    ///     <c>Local_variable_from_OnModelCreating_can_throw_exception</c> got EF's message minus
    ///     the expression that caused it — right exception, right place, two words different.
    ///     Deliberately not the rest of <c>AddOptions</c>: its
    ///     <c>ConfigureWarnings(Default(Throw))</c> is a statement about what the test author
    ///     wrote, and the server runs a tree this provider generated.
    /// </remarks>
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
    {
        builder = builder.UseInternalServiceProvider(ServiceProvider).EnableSensitiveDataLogging();

        return _testStoreProperties.OnAddOptions?.Invoke(builder) ?? builder;
    }

    /// <inheritdoc />
    public async Task<QueryDataResult> QueryDataAsync(
        QueryDataRequest request,
        DbContext clientContext,
        CancellationToken cancellationToken = default)
    {
        using var _ = WithClientContext(clientContext);
        return await _client.QueryDataAsync(request, clientContext, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SaveChangesResult> SaveChangesAsync(
        SaveChangesRequest request,
        DbContext clientContext,
        CancellationToken cancellationToken = default)
    {
        using var _ = WithClientContext(clientContext);
        return await _client.SaveChangesAsync(request, clientContext, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     The client context of the request currently in flight, for the server context factory
    ///     below to copy per-request parameters from.
    /// </summary>
    /// <remarks>
    ///     A context property the model reads — <c>NorthwindContext.TenantPrefix</c>, which a
    ///     query filter closes over — never reaches the wire: the client captures its tree before
    ///     EF applies query filters, and the *server* applies its own model's filter using its
    ///     own context. So the value has to be carried out of band. `AsyncLocal` because the
    ///     server is a singleton serving concurrent requests, and the scope is one request.
    /// </remarks>
    private static readonly AsyncLocal<DbContext?> CurrentClientContext = new();

    private static IDisposable WithClientContext(DbContext clientContext)
    {
        DbContext? previous = CurrentClientContext.Value;
        CurrentClientContext.Value = clientContext;
        return new Restore(() => CurrentClientContext.Value = previous);
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    // The transaction operations go through the envelope too. They are the ones with no payload
    // worth speaking of, which is exactly why they had never exercised the dispatch: a bug in the
    // operation discriminator for `ReleaseSavepoint` would have been invisible.

    /// <inheritdoc />
    public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _client.BeginTransactionAsync(cancellationToken);

    /// <inheritdoc />
    public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => _client.CommitTransactionAsync(transactionId, cancellationToken);

    /// <inheritdoc />
    public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => _client.RollbackTransactionAsync(transactionId, cancellationToken);

    /// <inheritdoc />
    public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => _client.CreateSavepointAsync(transactionId, name, cancellationToken);

    /// <inheritdoc />
    public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => _client.RollbackToSavepointAsync(transactionId, name, cancellationToken);

    /// <inheritdoc />
    public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => _client.ReleaseSavepointAsync(transactionId, name, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
        => _client.SupportsSavepointsAsync(transactionId, cancellationToken);

    private IInfoCarrierServer CreateServer()
        => ServiceProvider.GetRequiredService<IInfoCarrierServer>();
}
