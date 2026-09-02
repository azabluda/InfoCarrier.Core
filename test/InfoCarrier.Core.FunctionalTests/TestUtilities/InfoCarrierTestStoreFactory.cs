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
    ///     A factory for backend stores of a given provider.
    /// </summary>
    public delegate InfoCarrierBackendTestStore InfoCarrierBackendTestStoreFactory(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties);

    /// <summary>
    ///     The InMemory backend store factory.
    /// </summary>
    public static InfoCarrierBackendTestStoreFactory InMemory
        => (name, shared, props) => new InMemoryInfoCarrierBackendTestStore(name, shared, props);

    /// <summary>
    ///     The SQLite backend store factory (ADR-009 Tier B, the relational tier).
    /// </summary>
    public static InfoCarrierBackendTestStoreFactory Sqlite
        => (name, shared, props) => new SqliteInfoCarrierBackendTestStore(name, shared, props);

    private readonly Func<SharedTestStoreProperties> _props;
    private readonly InfoCarrierBackendTestStoreFactory _backendFactory;
    private readonly bool _relationalClientStore;

    private InfoCarrierTestStoreFactory(
        Func<SharedTestStoreProperties> props,
        InfoCarrierBackendTestStoreFactory backendFactory,
        bool relationalClientStore = false)
    {
        _props = props;
        _backendFactory = backendFactory;
        _relationalClientStore = relationalClientStore;
    }

    /// <summary>
    ///     The client shell this factory hands out.
    /// </summary>
    /// <remarks>
    ///     A <see cref="RelationalInfoCarrierTestStore" /> only where a fixture asked for one. See
    ///     that class for why the choice is per fixture rather than global.
    /// </remarks>
    private TestStore CreateClientStore(InfoCarrierBackendTestStore backend)
        => _relationalClientStore
            ? new RelationalInfoCarrierTestStore(backend)
            : new InfoCarrierTestStore(backend);

    /// <summary>
    ///     Creates a factory for the given backend + fixture properties. Fixtures typically cache
    ///     the result in a field (<c>??=</c>) so it is created once per fixture instance.
    /// </summary>
    public static ITestStoreFactory Create(
        InfoCarrierBackendTestStoreFactory backendFactory,
        Type contextType,
        Action<ModelBuilder, DbContext>? onModelCreating,
        Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? onAddOptions = null,
        Action<DbContext, DbContext>? copyDbContextParameters = null,
        Type? serverContextType = null,
        Func<IServiceCollection, IServiceCollection>? onAddServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        ServiceLifetime? serverOptionsLifetime = null,
        bool relationalClientStore = false,
        bool arbitrarySqlExecution = false)
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
        };

        return new InfoCarrierTestStoreFactory(() => props, backendFactory, relationalClientStore);
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
        InfoCarrierBackendTestStoreFactory backendFactory,
        Func<SharedTestStoreProperties> properties,
        bool relationalClientStore = false)
        => new InfoCarrierTestStoreFactory(properties, backendFactory, relationalClientStore);

    /// <inheritdoc />
    public virtual TestStore Create(string storeName)
        => CreateClientStore(_backendFactory(storeName, shared: false, _props()));

    /// <inheritdoc />
    public virtual TestStore GetOrCreate(string storeName)
        => CreateClientStore(_backendFactory(storeName, shared: true, _props()));

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

        // #56 option D, and gated on the SAME flag as the server's raw-SQL grant rather than on a
        // flag of its own. `Database.SqlQuery<T>` IS arbitrary SQL execution: without the server
        // half the call cannot work, so registering the facade shim without it would only trade one
        // exception for another. The two are wanted together or not at all.
        if (_props().ArbitrarySqlExecution)
        {
            serviceCollection = InfoCarrierRelationalFacadeDependencies
                .AddInfoCarrierRelationalFacade(serviceCollection);
        }

        return serviceCollection;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     A <see cref="TestSqlLoggerFactory" /> rather than a bare
    ///     <see cref="ListLoggerFactory" />, which it derives from. Several spec fixtures expose
    ///     <c>TestSqlLoggerFactory</c> as a non-virtual property that simply casts this one, and
    ///     their bases read it — `ComplexCollectionJsonUpdateTestBase.SuspendRecordingEvents()`
    ///     does, and failed all 18 of its tests on the cast before this. On a client with no
    ///     database it records no SQL and costs nothing; nothing in this suite asserts SQL.
    /// </remarks>
    public ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new TestSqlLoggerFactory(shouldLogCategory);
}
