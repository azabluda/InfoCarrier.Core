// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     An entity with a scalar, a complex property stored as columns, and a complex collection
///     stored as JSON, so that a write to one can be seen leaving the others alone.
/// </summary>
public class Shipment
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public Destination Destination { get; set; } = new();

    public List<Stop> Stops { get; set; } = [];
}

/// <summary>A complex type stored as columns of the owner's row.</summary>
public class Destination
{
    public string? City { get; set; }

    public string? Street { get; set; }
}

/// <summary>A complex type stored as elements of a JSON column.</summary>
public class Stop
{
    public string? Place { get; set; }
}

/// <summary>
///     A minimal context for the partial-update tests, on both client and server.
/// </summary>
/// <remarks>
///     A context of its own, like <see cref="ConcurrencyContext" />: the question is which columns
///     a write touches, and no other test should have to share that model.
/// </remarks>
public class PartialUpdateContext(DbContextOptions<PartialUpdateContext> options) : DbContext(options)
{
    public DbSet<Shipment> Shipments => Set<Shipment>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shipment>(b =>
        {
            b.Property(s => s.Id).ValueGeneratedNever();
            b.ComplexProperty(s => s.Destination);
            b.ComplexCollection(s => s.Stops, c => c.ToJson());
        });
    }
}
