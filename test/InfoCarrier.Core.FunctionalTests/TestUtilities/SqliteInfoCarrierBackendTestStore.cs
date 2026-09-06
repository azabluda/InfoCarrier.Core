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

            // POOLING OFF, AND THIS CLOSES AN INTERMITTENT RATHER THAN TIDYING SOMETHING.
            // `InitializeAsync` below calls `EnsureDeletedAsync`, and EF's
            // `SqliteDatabaseCreator.Delete` answers a file-backed database with
            // `SqliteConnection.ClearAllPools()` -- **process-wide**, not for this connection
            // string. It disposes every pooled native handle in the process, including one that a
            // concurrently initializing store is in the middle of opening, and that store then
            // fails inside `SqliteConnection.Open()` at `sqlite3_create_collation` with
            // `ObjectDisposedException: SQLitePCL.sqlite3`.
            //
            // Every SQLite store here deletes its file at initialization, so every one of them
            // fires that process-wide clear. xUnit runs test collections in parallel, and the two
            // ADR-009 tiers share one process since the test projects merged, so several stores
            // initialize at once. With no pool there is nothing for the clear to dispose.
            //
            // Observed once, as `AdHocMiscellaneousQuerySqliteInfoCarrierTest`
            // `.Bool_discriminator_column_works(async: False)`. The mechanism is read from EF's own
            // source and the failing stack rather than from a reproduction: a failure seen once
            // cannot be shown to have stopped.
            Pooling = false,
        }.ToString();
    }

    /// <summary>
    ///     Removes the database files a <em>previous</em> run left behind, once per process.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Nothing is deleted when a store is disposed. Doing that made disposal order
    ///         load-bearing again — the very thing making the store file-backed was meant to end —
    ///         and the test that ran next got "no such table". Sweeping at startup instead is safe
    ///         by construction: no store of this run has been created yet, so every file present is
    ///         stale, and each store deletes and recreates its own file at initialization anyway.
    ///     </para>
    ///     <para>
    ///         <b>A database here is three files, not one, and the sweep has to take all three.</b>
    ///         EF's <c>SqliteDatabaseCreator.Create</c> runs <c>PRAGMA journal_mode = 'wal'</c>, so
    ///         every store in this suite is a WAL database: the committed content lives in
    ///         <c>&lt;name&gt;.db-wal</c> until a checkpoint folds it back, and the <c>.db</c>
    ///         itself can be a 4 KB shell. Matching only <c>*.db</c> therefore swept a third of
    ///         each database and left the rest. <b>14,971 <c>-wal</c> and 14,946 <c>-shm</c> files
    ///         had accumulated against 76 <c>.db</c></b> when this was found, none of them ever
    ///         collected by anything. And a fresh <c>.db</c> opened beside a stale <c>-wal</c> and
    ///         <c>-shm</c> answers <c>SQLite Error 1: 'no such table'</c> — reproducible in
    ///         isolation: delete only the <c>.db</c>, reopen the path, and the WAL is silently
    ///         discarded.
    ///     </para>
    /// </remarks>
    private static void SweepStaleFiles()
    {
        // "*.db*", not "*.db": the -wal and -shm siblings are part of the database. See above.
        foreach (string stale in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.db*"))
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
    public override Type StoreParameterType => typeof(SqliteParameter);

    /// <inheritdoc />
    public override System.Data.Common.DbConnection CreateStoreConnection()
        => new SqliteConnection(_connectionString);

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

    /// <summary>
    ///     Whether the database at <see cref="_path" /> currently holds any table.
    /// </summary>
    /// <remarks>
    ///     Deliberately not <c>File.Exists</c>. Opening a SQLite path that has no file creates an
    ///     empty database rather than failing, so "the file is there" and "the database is there"
    ///     are different questions, and the second is the one that matters — an emptied database
    ///     and a missing one reach a test as the same <c>no such table</c>. Its own connection,
    ///     because the caller has none yet.
    /// </remarks>
    private bool HasTables()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table'";
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }
        catch (SqliteException)
        {
            // Unreadable is not usable, and the caller's answer to both is to build it again.
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         Guarded per file, and the guard is still needed even though the database now
    ///         outlives every connection: each backend store builds its <em>own</em> service
    ///         provider, so the <see cref="TestStoreIndex" /> that normally makes shared
    ///         initialization run once is not actually shared between them, and a second store
    ///         re-seeding the same file mid-run would destroy the first one's data.
    ///     </para>
    ///     <para>
    ///         <b>But the guard records that initialization was <em>started</em>, not that the
    ///         database is still there, and every later store trusted it forever.</b> That is what
    ///         produced this repository's only known intermittent (R76). Six classes share three
    ///         files here — <c>TPTFiltersInheritanceBulkUpdatesInfoCarrierFixture</c> and the two
    ///         other <c>…Filters…</c> fixtures override <c>EnableFilters</c> only, so each inherits
    ///         its parent's <c>StoreName</c>, exactly as EF's own suite does. The first class
    ///         creates and seeds the file, runs, and is disposed; the second is constructed
    ///         <em>two minutes later</em> and takes the branch below without looking. In between,
    ///         the file stops being protected: EF's <c>SqliteDatabaseCreator.Delete</c> calls
    ///         <c>SqliteConnection.ClearAllPools()</c> <em>process-globally</em> on every store's
    ///         initialization — some 646 times in a full run — and about fifteen seconds after the
    ///         first class ends, one of those drops the last handle and the file becomes deletable
    ///         by anything. Delete it in that window and the second class answers
    ///         <c>no such table</c> to every test that reaches the store, while the ones refused
    ///         before reaching it still pass: 18 failures of 27, which is the signature that was
    ///         seen once and then reproduced on demand.
    ///     </para>
    ///     <para>
    ///         So the branch verifies instead of trusting. A store that did not create the database
    ///         asks whether it is still there, and re-creates it when it is not. This costs one
    ///         <c>sqlite_master</c> count per skip (71 in a full run) and cannot destroy anyone's
    ///         data, because it only fires when the database has no tables at all — which no store
    ///         in this suite legitimately has once seeded.
    ///     </para>
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
            if (!Created.TryAdd(_path, true) && HasTables())
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
