using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Northwind.Shared;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     Hosts the sample server in-process and gives each test class its own SQLite file.
/// </summary>
/// <remarks>
///     A file, not <c>Mode=Memory;Cache=Shared</c>. CLAUDE.md records that making a database's
///     lifetime a connection's has already produced a 698-test phantom failure in this repo, and
///     the reason applies here too.
/// </remarks>
public sealed class NorthwindServerFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"northwind-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Pinned so the endpoint's error handling is exercised the same way regardless of ambient
        // hosting configuration (ASPNETCORE_ENVIRONMENT / DOTNET_ENVIRONMENT on the machine running
        // the tests). Under Development, ASP.NET Core's Developer Exception Page middleware would
        // otherwise turn an unhandled exception into an HTML page carrying a stack trace -- which,
        // before the endpoint caught its two known failure paths, is what made
        // An_unsupported_protocol_version_is_refused_by_number pass by accident (the stack trace
        // happened to contain the string "999"). Production is what a shipped server actually runs
        // under, and it is the strictest setting: no exception page, so a real 400 has to come from
        // the endpoint itself, not from hosting middleware.
        builder.UseEnvironment("Production");

        builder.ConfigureServices(services =>
        {
            ServiceDescriptor descriptor = services.Single(
                d => d.ServiceType == typeof(DbContextOptions<NorthwindContext>));
            services.Remove(descriptor);

            services.AddDbContext<NorthwindContext>(
                options => options
                    .UseSqlite($"Filename={_databasePath}")
                    .UseLazyLoadingProxies());
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);

        // Process-wide, not just this factory's pool: Microsoft.Data.Sqlite pools connections by
        // connection string, and this call clears every pool the process holds, including those
        // belonging to other NorthwindServerFactory instances xUnit may be running concurrently in
        // a different test class. That is acceptable rather than a hazard -- it only forces idle
        // pooled connections closed; a connection another test currently has open is unaffected and
        // simply reconnects the next time it is needed. It is what makes deleting *this* factory's
        // own file below reliable (see the M8-4 report for why: an open, unended transaction from
        // this project's own BeginTransaction test keeps a native handle open past host shutdown).
        SqliteConnection.ClearAllPools();

        // WAL mode (the default `Program.cs`/this factory both use) creates sidecar files next to
        // the main one; SQLite occasionally leaves a `-journal` behind too on unclean shutdown.
        // Deleting only the `.db` file left `-wal`/`-shm` pairs behind on every run.
        foreach (string path in new[]
        {
            _databasePath,
            _databasePath + "-wal",
            _databasePath + "-shm",
            _databasePath + "-journal",
        })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Best-effort: a test that opens a transaction and never commits or rolls it back
                // (there is one in this project on purpose) leaves InProcessInfoCarrierServer
                // holding that connection open across the whole host's lifetime -- it is a
                // singleton for Task 6's benefit and is not itself IDisposable, so nothing ends
                // the transaction when the host shuts down. Chasing that lock here would mean
                // reaching into product lifecycle management this task does not own; the files are
                // uniquely named per factory instance, so leaving them behind costs nothing but
                // temp-directory space.
            }
        }
    }
}
