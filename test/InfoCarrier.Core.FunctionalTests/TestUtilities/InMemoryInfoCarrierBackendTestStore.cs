// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Storage.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     An InMemory-backed <see cref="InfoCarrierBackendTestStore" /> — the first backend
///     (architecture §5: InMemory first, then SQL Server). The server context runs against
///     EF Core's InMemory provider.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="InMemoryInfoCarrierBackendTestStore" /> class.
/// </remarks>
public class InMemoryInfoCarrierBackendTestStore(
    string name,
    bool shared,
    SharedTestStoreProperties testStoreProperties) : InfoCarrierBackendTestStore(name, shared, testStoreProperties)
{

    /// <inheritdoc />
    /// <inheritdoc />
    /// <remarks>
    ///     ADR-009 Tier A runs over EF's InMemory provider, which is not relational and does not
    ///     refuse a <c>Distinct</c> over a projection carrying a collection. Enforcing that
    ///     relational rule here fails eight specification tests over a store that answers them.
    /// </remarks>
    public override bool ServerStoreIsRelational => false;

    protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
        => serviceCollection
            .AddEntityFrameworkInMemoryDatabase()
            .AddSingleton<TestStoreIndex>();

    /// <inheritdoc />
    protected override TestStoreIndex GetTestStoreIndex(IServiceProvider? serviceProvider)
        => serviceProvider?.GetService<TestStoreIndex>() ?? base.GetTestStoreIndex(serviceProvider);

    /// <inheritdoc />
    /// <remarks>
    ///     The ignored-transaction warning is logged rather than thrown, because since M4 the
    ///     client no longer decides that for itself: it asks the *store* to begin a transaction,
    ///     and this store is one that does not do them. EF's InMemory provider defaults that
    ///     warning to <c>WarningBehavior.Throw</c>, so without this every Tier A test that runs
    ///     inside <c>ExecuteWithStrategyInTransactionAsync</c> — most of the change-tracking
    ///     bases — would fail on `BeginTransaction` rather than on anything it was testing. The
    ///     client fixtures already opt in the same way, for the same reason.
    /// </remarks>
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => base.AddProviderOptions(builder)
            .UseInMemoryDatabase(Name)
            .ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning));

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
