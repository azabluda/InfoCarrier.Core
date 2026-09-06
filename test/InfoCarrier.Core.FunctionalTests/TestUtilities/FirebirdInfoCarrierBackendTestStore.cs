// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     A Firebird-backed <see cref="InfoCarrierBackendTestStore" /> — ADR-009 Tier C, the tier
///     that exists for what SQLite cannot express at all.
/// </summary>
/// <remarks>
///     <para>
///         <b>One capability justifies this tier: the table-valued function.</b> SQLite has none
///         and cannot be given one. <c>Microsoft.Data.Sqlite</c> registers a scalar delegate per
///         connection and exposes no <c>sqlite3_create_module</c>, so there are no virtual tables,
///         so <c>SELECT ... FROM SomeFunction(...)</c> has no meaning. That is the whole of
///         <c>UdfDbFunctionTestBase</c> Tier B leaves red, and the correlated form of it needs
///         <c>APPLY</c>, which SQLite also lacks. Firebird has both: a selectable stored procedure
///         is queried exactly as a table-valued function is, and <c>LATERAL</c> has been in the
///         engine since version 4.
///     </para>
///     <para>
///         <b>Nothing is installed and no server runs.</b> The engine arrives as a NuGet package
///         of native assets, and a database is one <c>.fdb</c> file in the test output directory,
///         exactly as Tier B's <c>.db</c> is. That was the condition this tier had to meet before
///         it could be built at all: no installation and no container.
///     </para>
///     <para>
///         <b>Everything else about the file's lifetime follows Tier B's store, and for Tier B's
///         reasons.</b> Stale files are swept once at startup, nothing is deleted on disposal, and
///         the created-guard verifies rather than trusts. Those three rules cost this repository a
///         698-test phantom failure and a nine-test intermittent to learn; see
///         <see cref="SqliteInfoCarrierBackendTestStore" />, which states each of them in full.
///     </para>
/// </remarks>
public class FirebirdInfoCarrierBackendTestStore : InfoCarrierBackendTestStore
{
    /// <summary>
    ///     The embedded engine's client library, resolved once per process.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The path is a real file under the test output directory. There is no registry
    ///         entry, no service, and no installation: the native assets package puts the binaries
    ///         in the build output and this reads back where they landed.
    ///     </para>
    ///     <para>
    ///         <b>Resolved here rather than by <c>FbNativeAssetManager.NativeAssetPath</c>, and
    ///         that is a Linux fix rather than a preference.</b> That helper starts from the
    ///         <em>process executable</em>. Under <c>dotnet test</c> on Windows the test host is an
    ///         executable sitting in the output directory, so it lands in the right place; on Linux
    ///         the host is the shared <c>dotnet</c> muxer, so it looks beside that instead and
    ///         finds nothing. It answers <see langword="null" /> either way, which reads as
    ///         "the package did not copy" and is not what happened: the binaries were copied
    ///         correctly and were being looked for in the wrong directory.
    ///         <see cref="AppContext.BaseDirectory" /> is the output directory on both platforms.
    ///     </para>
    /// </remarks>
    private static readonly Lazy<string> ClientLibrary = new(ResolveClientLibrary);

    // Keyed by file, exactly as Tier B's are, and concurrent for the same reason: the gate is per
    // file, so two files initialise at once.
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
    ///     Initializes a new instance of the <see cref="FirebirdInfoCarrierBackendTestStore" />
    ///     class.
    /// </summary>
    public FirebirdInfoCarrierBackendTestStore(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties)
        : base(name, shared, testStoreProperties)
    {
        _ = Swept.Value;

        _path = Path.GetFullPath(shared ? $"{name}.fdb" : $"{name}.{Guid.NewGuid():N}.fdb");
        _connectionString = new FbConnectionStringBuilder
        {
            Database = _path,
            ServerType = FbServerType.Embedded,
            UserID = "SYSDBA",
            ClientLibrary = ClientLibrary.Value,
            Charset = "UTF8",

            // Embedded Firebird holds the file for the life of a connection, and this suite opens
            // many contexts against one store. Pooling keeps a connection alive past the block
            // that closed it, which turns the next open into a lock conflict rather than a wait.
            // Tier B switched pooling off as well, for a different reason it records in full.
            Pooling = false,
        }.ToString();
    }

    /// <summary>
    ///     Finds the embedded client library in the test output directory.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Only one platform's binaries are ever present: each native assets package copies
    ///         under <c>IsOSPlatform</c>, so a Windows build has <c>win-x64</c> and nothing else.
    ///         Probing both is therefore unambiguous, and it keeps the platform test in one place.
    ///     </para>
    ///     <para>
    ///         <b><c>FIREBIRD</c> is set because the engine finds everything else relative to
    ///         it.</b> The client library is only the front door; <c>firebird.conf</c>, the engine
    ///         plugin, the character-set module and the time-zone data are located from the server
    ///         root, and an embedded engine that cannot find them fails at connection time rather
    ///         than at load time. Set only when the environment does not already name one, so a
    ///         machine with a real Firebird installation is left alone.
    ///     </para>
    ///     <para>
    ///         <b>The failure message lists what is actually there.</b> A tier whose binaries did
    ///         not arrive should say so in the terms a reader can act on, which is the directory
    ///         contents and not the expected path alone.
    ///     </para>
    /// </remarks>
    private static string ResolveClientLibrary()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "firebird");

        foreach ((string rid, string relative) in ((string, string)[])
            [
                ("win-x64", "fbclient.dll"),
                ("linux-x64", "lib/libfbclient.so.2"),
            ])
        {
            string serverRoot = Path.Combine(root, rid, "V5");
            string library = Path.Combine(serverRoot, relative);
            if (!File.Exists(library))
            {
                continue;
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIREBIRD")))
            {
                Environment.SetEnvironmentVariable("FIREBIRD", serverRoot);
            }

            return library;
        }

        string present = Directory.Exists(root)
            ? string.Join(
                ", ",
                Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
                    .Select(entry => Path.GetRelativePath(root, entry))
                    .Take(40))
            : "the directory does not exist";

        throw new InvalidOperationException(
            $"No embedded Firebird client library under '{root}', so ADR-009 Tier C cannot run. "
            + "The binaries arrive by package reference and each package copies only on its own "
            + $"platform, so a machine that is neither Windows nor Linux x64 gets nothing. Found: {present}");
    }

    /// <summary>
    ///     Removes the database files a <em>previous</em> run left behind, once per process.
    /// </summary>
    /// <remarks>
    ///     A Firebird database is one file, unlike a SQLite one, so this takes only
    ///     <c>*.fdb</c>. Nothing is deleted on disposal, for the reason
    ///     <see cref="SqliteInfoCarrierBackendTestStore" /> gives at length: it makes disposal
    ///     order load-bearing between classes that share a store name.
    /// </remarks>
    private static void SweepStaleFiles()
    {
        foreach (string stale in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.fdb"))
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
    public override Type StoreParameterType => typeof(FbParameter);

    /// <inheritdoc />
    public override System.Data.Common.DbConnection CreateStoreConnection()
        => new FbConnection(_connectionString);

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         <b>The SQL generator is replaced here rather than on the options, and it has to
    ///         be.</b> <c>AddProviderOptions</c> calls <c>UseInternalServiceProvider</c>, and EF
    ///         refuses <c>ReplaceService</c> alongside one: "Entity Framework is not building its
    ///         own internal service provider". The replacement therefore belongs in the collection
    ///         that provider is built from, which is this one.
    ///     </para>
    ///     <para>
    ///         <see cref="FirebirdLateralQuerySqlGeneratorFactory" /> says what it corrects and
    ///         why the correction is somebody else's to make permanent. <c>RemoveAll</c> first,
    ///         because the provider registered its own with <c>TryAdd</c> and a second
    ///         registration would not win.
    ///     </para>
    /// </remarks>
    protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        serviceCollection
            .AddEntityFrameworkFirebird()
            .AddSingleton<TestStoreIndex>();

        serviceCollection.RemoveAll<IQuerySqlGeneratorFactory>();
        serviceCollection.AddSingleton<IQuerySqlGeneratorFactory, FirebirdLateralQuerySqlGeneratorFactory>();

        return serviceCollection;
    }

    /// <inheritdoc />
    protected override TestStoreIndex GetTestStoreIndex(IServiceProvider? serviceProvider)
        => serviceProvider?.GetService<TestStoreIndex>() ?? base.GetTestStoreIndex(serviceProvider);

    /// <inheritdoc />
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => base.AddProviderOptions(builder).UseFirebird(_connectionString);

    /// <summary>
    ///     Whether the database at <c>_path</c> currently holds any user table.
    /// </summary>
    /// <remarks>
    ///     Deliberately not <c>File.Exists</c>, for the reason Tier B's store gives: an emptied
    ///     database and a missing one reach a test as the same failure, and only the second
    ///     question matters. The system-flag clause excludes Firebird's own catalogue, which is
    ///     present in every database ever created.
    /// </remarks>
    private bool HasTables()
    {
        try
        {
            using var connection = new FbConnection(_connectionString);
            connection.Open();
            using FbCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT count(*) FROM RDB$RELATIONS WHERE COALESCE(RDB$SYSTEM_FLAG, 0) = 0";
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }
        catch (FbException)
        {
            // Unreadable is not usable, and the caller's answer to both is to build it again.
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The guard verifies rather than trusts, which is R76's rule: a record that
    ///     initialization <em>started</em> is not evidence its result still exists.
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
            // otherwise be seeded a second time.
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
}
