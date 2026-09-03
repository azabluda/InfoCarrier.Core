// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     An entity with an application-managed concurrency token — the shape SQLite supports,
///     since it has no native row version.
/// </summary>
public class Widget
{
    public int Id { get; set; }

    public string? Name { get; set; }

    /// <summary>
    ///     The concurrency token. The application bumps it on each write, which is the case that
    ///     distinguishes a real conflict from a token the client changed itself.
    /// </summary>
    public int Version { get; set; }
}

/// <summary>
///     A minimal context for the concurrency-token tests, on both client and server.
/// </summary>
/// <remarks>
///     A context of its own rather than a property added to <see cref="SqliteSmokeContext" />:
///     a concurrency token changes how every write to that entity is issued, and the smoke tests
///     are not about that.
/// </remarks>
public class ConcurrencyContext(DbContextOptions<ConcurrencyContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(b =>
        {
            b.Property(w => w.Id).ValueGeneratedNever();
            b.Property(w => w.Version).IsConcurrencyToken();
        });
    }
}
