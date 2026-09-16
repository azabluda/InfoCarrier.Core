// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     A row with everything the statements this suite promises are about: a scalar, a navigation,
///     a complex value in columns, a complex collection in JSON, and a concurrency token.
/// </summary>
public class Ticket
{
    public int Id { get; set; }

    public string? Subject { get; set; }

    public int Priority { get; set; }

    /// <summary>A complex value stored as columns of this row. <c>Hall</c> is the concurrency token.</summary>
    public Venue Venue { get; set; } = new();

    /// <summary>A complex collection stored as one JSON column of this row.</summary>
    public List<Marker> Markers { get; set; } = [];

    /// <summary>
    ///     An owned reference in this row, whose <c>Badge</c> is a concurrency token. An owned type
    ///     is an entry of its own in the change tracker, which is what makes it the shape a token can
    ///     be dropped from.
    /// </summary>
    public Holder Holder { get; set; } = new();

    /// <summary>A navigation to another table, so a join and a split query have something to join.</summary>
    public List<Seat> Seats { get; set; } = [];
}

/// <summary>A row on the other side of the navigation.</summary>
public class Seat
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public string? Label { get; set; }
}

/// <summary>A complex type in the owner's columns, carrying the concurrency token.</summary>
public class Venue
{
    public string? Hall { get; set; }

    public string? Row { get; set; }
}

/// <summary>An owned type in the owner's row, and an entry of its own.</summary>
public class Holder
{
    public string? Name { get; set; }

    public string? Badge { get; set; }
}

/// <summary>A complex type in the owner's JSON column.</summary>
public class Marker
{
    public string? Name { get; set; }
}

/// <summary>
///     The model behind <c>ServerSqlTest</c>, on the client and on the server.
/// </summary>
/// <remarks>
///     <b>A model of its own, and short names on purpose.</b> The tests read a statement to check a
///     promise, so the statement has to be readable: a Northwind row prints eleven columns before
///     the interesting clause arrives. It is also ours, so nobody else's change rewrites what these
///     tests expect.
/// </remarks>
public class ServerSqlContext(DbContextOptions<ServerSqlContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<Seat> Seats => Set<Seat>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(b =>
        {
            b.Property(t => t.Id).ValueGeneratedNever();
            b.ComplexProperty(t => t.Venue, v => v.Property(p => p.Hall).IsConcurrencyToken());
            b.ComplexCollection(t => t.Markers, c => c.ToJson());
            b.OwnsOne(t => t.Holder, h => h.Property(p => p.Badge).IsConcurrencyToken());
            b.HasMany(t => t.Seats).WithOne().HasForeignKey(s => s.TicketId);
        });

        modelBuilder.Entity<Seat>(b => b.Property(s => s.Id).ValueGeneratedNever());
    }
}
