// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
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
///         Tier A (InMemory) cannot test transactions at all, and it client-evaluates almost
///         everything, which makes it a poor judge of what a real provider can translate —
///         several failures against Tier A are InMemory's limits rather than this provider's, and
///         only a relational backend can tell them apart.
///     </para>
///     <para>
///         <b>The database is a file, as EF Core's own <c>SqliteTestStore</c> makes it.</b> This
///         store used to open <c>Mode=Memory;Cache=Shared</c> and hold one connection open for
///         its lifetime, because an in-memory SQLite database is destroyed when its last
///         connection closes. That made <em>disposal order</em> load-bearing across test classes,
///         and it produced a 698-test phantom failure: <c>NorthwindWhereQuerySqlite…</c> and
///         <c>NorthwindSelectQuerySqlite…</c> take the same fixture <em>type</em>, so xUnit builds
///         one fixture instance per class and both ask for the store named "Northwind". The first
///         created and seeded it; the second skipped creation (see the guard below) and then
///         queried a database the first had already destroyed by disposing. Adding 1787
///         GraphUpdates tests changed the scheduling enough to expose it.
///     </para>
///     <para>
///         A file's lifetime is its own. No connection has to be held, nothing is destroyed by
///         closing one, and every context opens its own — so the store no longer shares a single
///         <see cref="SqliteConnection" /> across concurrent contexts either. The files land in
///         the test output directory, which is git-ignored, exactly as EF's <c>northwind.db</c>
///         does.
///     </para>
/// </remarks>
public class SqliteInfoCarrierBackendTestStore : InfoCarrierBackendTestStore
{
    // Keyed by file, which is what identifies the database. Both are concurrent: the gate is
    // per file, so two files initialise at once and a plain HashSet would be raced.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();
    private static readonly ConcurrentDictionary<string, bool> Created = new();

    // Runs before the first store of this process exists, so everything it finds is stale.
    private static readonly Lazy<bool> Swept = new(() =>
    {
        SweepStaleFiles();
        return true;
    });

    private readonly string _path;
    private readonly string _connectionString;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SqliteInfoCarrierBackendTestStore" /> class.
    /// </summary>
    public SqliteInfoCarrierBackendTestStore(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties)
        : base(name, shared, testStoreProperties)
    {
        _ = Swept.Value;

        // A shared store is identified by its name, so every fixture asking for that name gets
        // the same file. An unshared one asked for isolation and gets a file of its own — the
        // smoke tests create a store per test and would otherwise trample each other.
        _path = Path.GetFullPath(shared ? $"{name}.db" : $"{name}.{Guid.NewGuid():N}.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Cache = SqliteCacheMode.Private,
        }.ToString();
    }

    /// <summary>
    ///     Removes the database files a <em>previous</em> run left behind, once per process.
    /// </summary>
    /// <remarks>
    ///     Nothing is deleted when a store is disposed. Doing that made disposal order
    ///     load-bearing again — the very thing making the store file-backed was meant to end —
    ///     and the test that ran next got "no such table". Sweeping at startup instead is safe by
    ///     construction: no store of this run has been created yet, so every file present is
    ///     stale, and each store deletes and recreates its own file at initialization anyway.
    /// </remarks>
    private static void SweepStaleFiles()
    {
        foreach (string stale in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.db"))
        {
            try
            {
                File.Delete(stale);
            }
            catch (IOException)
            {
                // A file another process holds is not ours to reclaim.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
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
        => base.AddProviderOptions(builder).UseSqlite(_connectionString);

    /// <inheritdoc />
    /// <remarks>
    ///     Guarded per file, and the guard is still needed even though the database now outlives
    ///     every connection: each backend store builds its <em>own</em> service provider, so the
    ///     <see cref="TestStoreIndex" /> that normally makes shared initialization run once is not
    ///     actually shared between them, and a second store re-seeding the same file mid-run
    ///     would destroy the first one's data.
    /// </remarks>
    protected override async Task InitializeAsync(
        Func<DbContext> createContext,
        Func<DbContext, Task>? seed,
        Func<DbContext, Task>? clean)
    {
        SemaphoreSlim gate = Gates.GetOrAdd(_path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Created.TryAdd(_path, true))
            {
                return;
            }

            using DbContext context = createContext();

            // A file survives the process that made it, so a previous run's database would
            // otherwise be seeded a second time. `EnsureDeleted` on SQLite deletes the file.
            await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
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
    /// <remarks>
    ///     <para>
    ///         Disposal releases nothing. The file is deliberately not deleted — doing that made
    ///         the store's disposal order load-bearing all over again, and the next test got "no
    ///         such table" despite having created and seeded a file of its own.
    ///     </para>
    ///     <para>
    ///         <b>Nor is the <see cref="Created" /> entry removed, for the same reason.</b> That
    ///         was the surviving half of the coupling and it stayed hidden until the suite grew
    ///         past ten thousand tests: several classes share the store named "Northwind" and so
    ///         share one file, each builds its own service provider (so the
    ///         <see cref="TestStoreIndex" /> that would normally make shared initialization run
    ///         once is not shared between them), and <see cref="Created" /> was the only thing
    ///         left stopping a second one from re-initializing. Removing the entry on disposal
    ///         re-armed it: one class finishing while another still held the file let a third
    ///         pass the guard and run <c>EnsureDeleted</c> + <c>EnsureCreated</c> + seed,
    ///         deleting the database out from under the class still using it. It showed up as
    ///         nine <c>NorthwindWhereQuerySqlite…</c> tests failing in one run of the full suite
    ///         and passing in the next, with no code change between them.
    ///     </para>
    ///     <para>
    ///         A file initialized once is initialized for the lifetime of the process, which is
    ///         what "shared" is supposed to mean. An unshared store has a path of its own and is
    ///         unaffected either way.
    ///     </para>
    /// </remarks>
    public override ValueTask DisposeAsync()
        => base.DisposeAsync();
}
