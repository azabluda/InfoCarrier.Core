// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     A SQLite-backed <see cref="InfoCarrierBackendTestStore" /> — ADR-009 Tier B, the
///     relational tier.
/// </summary>
/// <remarks>
///     <para>
///         Tier A (InMemory) cannot test transactions at all: EF's InMemory provider registers
///         <c>TransactionIgnoredWarning</c> with <c>WarningBehavior.Throw</c>. It also client-
///         evaluates almost everything, which makes it a poor judge of what a real provider can
///         translate — several failures against Tier A are InMemory's limits rather than this
///         provider's, and only a relational backend can tell them apart.
///     </para>
///     <para>
///         <b>The connection is held open for the store's lifetime.</b> An in-memory SQLite
///         database is destroyed when its last connection closes, so letting EF open and close
///         per context would discard the schema and the seed between operations.
///     </para>
/// </remarks>
public class SqliteInfoCarrierBackendTestStore : InfoCarrierBackendTestStore
{
    private readonly SqliteConnection _connection;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SqliteInfoCarrierBackendTestStore" /> class.
    /// </summary>
    public SqliteInfoCarrierBackendTestStore(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties)
        : base(name, shared, testStoreProperties)
    {
        // A shared cache plus a held-open connection is what keeps one in-memory database
        // addressable from every context the store hands out.
        _connection = new SqliteConnection($"DataSource={name};Mode=Memory;Cache=Shared");
        _connection.Open();
    }

    /// <inheritdoc />
    protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
        => serviceCollection
            .AddEntityFrameworkSqlite()
            .AddSingleton<TestStoreIndex>();

    /// <inheritdoc />
    protected override TestStoreIndex GetTestStoreIndex(IServiceProvider? serviceProvider)
        => serviceProvider?.GetService<TestStoreIndex>() ?? base.GetTestStoreIndex(serviceProvider);

    /// <inheritdoc />
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => base.AddProviderOptions(builder).UseSqlite(_connection);

    /// <inheritdoc />
    protected override async Task InitializeAsync(
        Func<DbContext> createContext,
        Func<DbContext, Task>? seed,
        Func<DbContext, Task>? clean)
    {
        using DbContext context = createContext();
        await context.Database.EnsureCreatedAsync().ConfigureAwait(false);

        if (clean is not null)
        {
            await clean(context).ConfigureAwait(false);
        }

        if (seed is not null)
        {
            await seed(context).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
