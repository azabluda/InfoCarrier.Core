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
        => builder.ConfigureServices(services =>
        {
            ServiceDescriptor descriptor = services.Single(
                d => d.ServiceType == typeof(DbContextOptions<NorthwindContext>));
            services.Remove(descriptor);

            services.AddDbContext<NorthwindContext>(
                options => options
                    .UseSqlite($"Filename={_databasePath}")
                    .UseLazyLoadingProxies());
        });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);

        if (File.Exists(_databasePath))
        {
            SqliteConnection.ClearAllPools();

            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
                // Best-effort: a test that opens a transaction and never commits or rolls it back
                // (there is one in this project on purpose) leaves InProcessInfoCarrierServer
                // holding that connection open across the whole host's lifetime — it is a
                // singleton for Task 6's benefit and is not itself IDisposable, so nothing ends
                // the transaction when the host shuts down. Chasing that lock here would mean
                // reaching into product lifecycle management this task does not own; the file is
                // uniquely named per factory instance, so leaving it behind costs nothing but
                // temp-directory space.
            }
        }
    }
}
