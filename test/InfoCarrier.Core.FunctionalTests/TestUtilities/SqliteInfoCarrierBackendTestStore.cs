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
    // Keyed by store name, which is what identifies the shared in-memory database. Both are
    // concurrent: the gate is per name, so two names initialise at once and a plain HashSet
    // would be raced.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> Created = new();

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
        // Every context this store hands out uses this one connection object, so a private
        // in-memory database suffices — and a shared cache is joined only when the store really
        // is shared. `Cache=Shared` is process-wide and keyed by name, so an unshared store that
        // opted into it could be reached by anything else running in parallel; that is what made
        // the smoke tests fail only when other SQLite classes ran alongside them.
        _connection = new SqliteConnection(
            shared
                ? $"DataSource={name};Mode=Memory;Cache=Shared"
                : "DataSource=:memory:");
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
    /// <remarks>
    ///     Guarded per store name, which is what identifies the shared in-memory database.
    ///     Each backend store builds its <em>own</em> service provider, so the
    ///     <see cref="TestStoreIndex" /> that normally makes shared initialization run once is
    ///     not actually shared between them — every test class re-entered this method for the
    ///     same database. On InMemory that is harmless; on a relational store the second
    ///     <c>EnsureCreated</c> reports <c>table "CustomerQueries" already exists</c>, which is
    ///     what 534 failures turned out to be.
    /// </remarks>
    protected override async Task InitializeAsync(
        Func<DbContext> createContext,
        Func<DbContext, Task>? seed,
        Func<DbContext, Task>? clean)
    {
        SemaphoreSlim gate = Gates.GetOrAdd(Name, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Created.TryAdd(Name, true))
            {
                return;
            }

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
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
