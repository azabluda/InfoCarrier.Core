// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory.Storage.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     An InMemory-backed <see cref="InfoCarrierBackendTestStore" /> — the first backend
///     (architecture §5: InMemory first, then SQL Server). The server context runs against
///     EF Core's InMemory provider.
/// </summary>
public class InMemoryInfoCarrierBackendTestStore : InfoCarrierBackendTestStore
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InMemoryInfoCarrierBackendTestStore" /> class.
    /// </summary>
    public InMemoryInfoCarrierBackendTestStore(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties)
        : base(name, shared, testStoreProperties)
    {
    }

    /// <inheritdoc />
    protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
        => serviceCollection
            .AddEntityFrameworkInMemoryDatabase()
            .AddSingleton<TestStoreIndex>();

    /// <inheritdoc />
    protected override TestStoreIndex GetTestStoreIndex(IServiceProvider? serviceProvider)
        => serviceProvider?.GetService<TestStoreIndex>() ?? base.GetTestStoreIndex(serviceProvider);

    /// <inheritdoc />
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => base.AddProviderOptions(builder).UseInMemoryDatabase(Name);

    /// <inheritdoc />
    /// <remarks>
    ///     Verbatim from EF Core's own <c>InMemoryTestStore</c>. Nothing needed this while the
    ///     only fixtures were query fixtures — a query never dirties the store — but a fixture
    ///     that reseeds between tests, as the change-tracking bases do, appends to the previous
    ///     seed unless the store is actually emptied first.
    /// </remarks>
    public override Task CleanAsync(DbContext context)
    {
        context.GetService<IInMemoryStoreProvider>().Store.Clear();
        return context.Database.EnsureCreatedAsync();
    }
}
