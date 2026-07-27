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

    private readonly SharedTestStoreProperties _props;
    private readonly InfoCarrierBackendTestStoreFactory _backendFactory;

    private InfoCarrierTestStoreFactory(
        SharedTestStoreProperties props,
        InfoCarrierBackendTestStoreFactory backendFactory)
    {
        _props = props;
        _backendFactory = backendFactory;
    }

    /// <summary>
    ///     Thread-safe lazy singleton per fixture (v1's NonCapturingLazyInitializer pattern,
    ///     reimplemented since that helper is EF-internal).
    /// </summary>
    public static ITestStoreFactory EnsureInitialized(
        ref ITestStoreFactory? instance,
        InfoCarrierBackendTestStoreFactory backendFactory,
        Type contextType,
        Action<ModelBuilder, DbContext>? onModelCreating,
        Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? onAddOptions = null,
        Action<DbContext, DbContext>? copyDbContextParameters = null)
    {
        if (instance is not null)
        {
            return instance;
        }

        var props = new SharedTestStoreProperties
        {
            ContextType = contextType,
            OnModelCreating = onModelCreating,
            OnAddOptions = onAddOptions,
            CopyDbContextParameters = copyDbContextParameters,
        };

        Interlocked.CompareExchange(
            ref instance,
            new InfoCarrierTestStoreFactory(props, backendFactory),
            null);
        return instance;
    }

    /// <inheritdoc />
    public virtual TestStore Create(string storeName)
        => new InfoCarrierTestStore(_backendFactory(storeName, shared: false, _props));

    /// <inheritdoc />
    public virtual TestStore GetOrCreate(string storeName)
        => new InfoCarrierTestStore(_backendFactory(storeName, shared: true, _props));

    /// <inheritdoc />
    public IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkInfoCarrier();

    /// <inheritdoc />
    public ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new(shouldLogCategory);
}
