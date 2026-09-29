// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     An entity whose complex property is a property bag, one of whose members is a list.
/// </summary>
public class Crew
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public Dictionary<string, object> Team { get; set; } = [];
}

/// <summary>
///     A minimal context for the property-bag tests, on both client and server.
/// </summary>
/// <remarks>
///     The shape of EF's <c>PubWithPropertyBagCollections.FeaturedTeam</c>, cut to the two members
///     that matter: a property-bag complex type whose members are indexer properties, and among
///     them a primitive collection, which is what sends EF's materializer down the branch #52 is
///     about.
/// </remarks>
public class PropertyBagContext(DbContextOptions<PropertyBagContext> options) : DbContext(options)
{
    public DbSet<Crew> Crews => Set<Crew>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Crew>(b =>
        {
            b.Property(c => c.Id).ValueGeneratedNever();
            b.ComplexProperty(
                c => c.Team,
                "CrewTeam",
                t =>
                {
                    t.Property<string>("Name");
                    t.Property<List<string>>("Members");
                });
        });
}
