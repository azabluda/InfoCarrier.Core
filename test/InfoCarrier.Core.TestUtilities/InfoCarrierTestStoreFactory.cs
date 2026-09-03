// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The <see cref="ITestStoreFactory" /> that builds InfoCarrier client stores backed by a
///     real provider (v1 pattern). Captures fixture state via <see cref="SharedTestStoreProperties" />
///     because the factory members take only a store name.
/// </summary>
public class InfoCarrierTestStoreFactory : ITestStoreFactory
{
    /// <summary>
    ///     ADR-009 Tier A: EF's InMemory provider, which is not a relational store.
    /// </summary>
    /// <remarks>
    ///     Tier B is <c>SqliteInfoCarrierTier</c>, which lives beside the <c>Sqlite/</c> tests it
    ///     serves and is named there. It used to be a second static on this class, which meant this
    ///     store-neutral project referenced a relational provider and so did everything referencing
    ///     it. See <see cref="InfoCarrierTier" />.
    /// </remarks>
    public static InfoCarrierTier InMemory { get; } = new InMemoryInfoCarrierTier();

    private readonly Func<SharedTestStoreProperties> _props;
    private readonly InfoCarrierTier _tier;
    private readonly bool _relationalClientStore;

    private InfoCarrierTestStoreFactory(
        Func<SharedTestStoreProperties> props,
        InfoCarrierTier tier,
        bool relationalClientStore = false)
    {
        _props = props;
        _tier = tier;
        _relationalClientStore = relationalClientStore;
    }

    /// <summary>
    ///     Creates a factory for the given backend + fixture properties. Fixtures typically cache
    ///     the result in a field (<c>??=</c>) so it is created once per fixture instance.
    /// </summary>
    public static ITestStoreFactory Create(
        InfoCarrierTier tier,
        Type contextType,
        Action<ModelBuilder, DbContext>? onModelCreating,
        Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? onAddOptions = null,
        Action<DbContext, DbContext>? copyDbContextParameters = null,
        Type? serverContextType = null,
        Func<IServiceCollection, IServiceCollection>? onAddServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        ServiceLifetime? serverOptionsLifetime = null,
        bool relationalClientStore = false,
        bool arbitrarySqlExecution = false,
        Type[]? allowedTypes = null)
    {
        var props = new SharedTestStoreProperties
        {
            ContextType = contextType,
            ServerContextType = serverContextType,
            OnModelCreating = onModelCreating,
            ConfigureConventions = configureConventions,
            OnAddOptions = onAddOptions,
            CopyDbContextParameters = copyDbContextParameters,
            OnAddServices = onAddServices,
            ServerOptionsLifetime = serverOptionsLifetime,
            ArbitrarySqlExecution = arbitrarySqlExecution,
            AllowedTypes = allowedTypes,
        };

        return new InfoCarrierTestStoreFactory(() => props, tier, relationalClientStore);
    }

    /// <summary>
    ///     Creates a factory whose properties are read <em>at store-creation time</em>.
    /// </summary>
    /// <remarks>
    ///     A <c>NonSharedModelTestBase</c> builds a different <see cref="DbContext" /> type per
    ///     test, so the server context type and model customization are not known when the fixture
    ///     builds its factory — only when a test calls <c>InitializeAsync&lt;TContext&gt;</c>. This
    ///     overload lets <see cref="NonSharedModelInfoCarrierTestBase" /> supply them then. Every
    ///     other fixture has one context type for its lifetime and uses the overload above.
    /// </remarks>
    public static ITestStoreFactory CreateDeferred(
        InfoCarrierTier tier,
        Func<SharedTestStoreProperties> properties,
        bool relationalClientStore = false)
        => new InfoCarrierTestStoreFactory(properties, tier, relationalClientStore);

    /// <summary>
    ///     The client shell this factory hands out, from the tier that knows what one looks like.
    /// </summary>
    private TestStore CreateClientStore(InfoCarrierBackendTestStore backend)
        => _tier.CreateClientStore(backend, _relationalClientStore);

    /// <inheritdoc />
    public virtual TestStore Create(string storeName)
        => CreateClientStore(_tier.CreateBackend(storeName, shared: false, _props()));

    /// <inheritdoc />
    public virtual TestStore GetOrCreate(string storeName)
        => CreateClientStore(_tier.CreateBackend(storeName, shared: true, _props()));

    /// <inheritdoc />
    /// <remarks>
    ///     The geometry mapper is the client half of ADR-012's seam, and it is registered
    ///     <em>here</em> — in the test utilities — rather than in the product, which is how v1
    ///     kept NetTopologySuite out of `InfoCarrier.Core` and how this provider keeps it out
    ///     too. Registered for every fixture, not only the spatial ones: a mapper that is not
    ///     handed a geometry declines, so the cost is one type test per non-primitive value and
    ///     the benefit is that the seam is exercised by the whole suite rather than by two
    ///     classes. See <see cref="InfoCarrierBackendTestStore" /> for the server half.
    /// </remarks>
    public IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
    {
        serviceCollection = serviceCollection
            .AddEntityFrameworkInfoCarrier()
            .AddSingleton<InfoCarrier.Core.ValueMapping.IInfoCarrierValueMapper, InfoCarrierNetTopologySuiteValueMapper>();

        // Whatever else the tier's own store needs on the CLIENT. The relational tier registers
        // `AddInfoCarrierRelationalClient()` here (#56 option D), gated on the same raw-SQL grant
        // as its server half: `Database.SqlQuery<T>` IS arbitrary SQL execution, so the facade shim
        // without the server half would only trade one exception for another. This assembly cannot
        // name that call, which is the point -- see `InfoCarrierTier`.
        return _tier.AddClientServices(serviceCollection, _props().ArbitrarySqlExecution);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The tier decides. The relational one returns a <c>TestSqlLoggerFactory</c>, because
    ///     several relational spec fixtures cast to it without asking; the store-neutral default is
    ///     a plain <see cref="ListLoggerFactory" />. On a client with no database neither records
    ///     any SQL, and nothing in this suite asserts on it.
    /// </remarks>
    public ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => _tier.CreateListLoggerFactory(shouldLogCategory);
}
