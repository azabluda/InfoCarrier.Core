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
///     An entity whose concurrency tokens are the members of a complex property, as
///     <c>Engine.StorageLocation</c> is in EF's own optimistic-concurrency model.
/// </summary>
public class Crate
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public Place Place { get; set; } = new();
}

/// <summary>
///     The complex type whose members are concurrency tokens.
/// </summary>
public class Place
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }
}

/// <summary>
///     An entity whose concurrency tokens are the members of an OWNED reference in the owner's
///     table, which is how EF's own optimistic-concurrency model maps <c>Engine.StorageLocation</c>.
/// </summary>
public class Parcel
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public Spot Spot { get; set; } = new();
}

/// <summary>
///     The owned type whose members are concurrency tokens.
/// </summary>
public class Spot
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }
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

    public DbSet<Crate> Crates => Set<Crate>();

    public DbSet<Parcel> Parcels => Set<Parcel>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(b =>
        {
            b.Property(w => w.Id).ValueGeneratedNever();
            b.Property(w => w.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<Crate>(b =>
        {
            b.Property(c => c.Id).ValueGeneratedNever();
            b.ComplexProperty(c => c.Place, p =>
            {
                p.Property(l => l.Latitude).IsConcurrencyToken();
                p.Property(l => l.Longitude).IsConcurrencyToken();
            });
        });

        modelBuilder.Entity<Parcel>(b =>
        {
            b.Property(c => c.Id).ValueGeneratedNever();
            b.OwnsOne(c => c.Spot, s =>
            {
                s.Property(l => l.Latitude).IsConcurrencyToken();
                s.Property(l => l.Longitude).IsConcurrencyToken();
            });
        });
    }
}
