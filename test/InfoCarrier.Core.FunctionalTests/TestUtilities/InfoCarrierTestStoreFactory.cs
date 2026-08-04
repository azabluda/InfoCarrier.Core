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

    private InfoCarrierTestStoreFactory(
        Func<SharedTestStoreProperties> props,
        InfoCarrierBackendTestStoreFactory backendFactory)
    {
        _props = props;
        _backendFactory = backendFactory;
    }

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
        Func<IServiceCollection, IServiceCollection>? onAddServices = null)
    {
        var props = new SharedTestStoreProperties
        {
            ContextType = contextType,
            ServerContextType = serverContextType,
            OnModelCreating = onModelCreating,
            OnAddOptions = onAddOptions,
            CopyDbContextParameters = copyDbContextParameters,
            OnAddServices = onAddServices,
        };

        return new InfoCarrierTestStoreFactory(() => props, backendFactory);
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
        Func<SharedTestStoreProperties> properties)
        => new InfoCarrierTestStoreFactory(properties, backendFactory);

    /// <inheritdoc />
    public virtual TestStore Create(string storeName)
        => new InfoCarrierTestStore(_backendFactory(storeName, shared: false, _props()));

    /// <inheritdoc />
    public virtual TestStore GetOrCreate(string storeName)
        => new InfoCarrierTestStore(_backendFactory(storeName, shared: true, _props()));

    /// <inheritdoc />
    public IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkInfoCarrier();

    /// <inheritdoc />
    public ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new(shouldLogCategory);
}
