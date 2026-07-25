// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     A minimal entity + context for the first end-to-end smoke test. The full Northwind
///     spec-test fixture lands after the vertical slice is proven.
/// </summary>
public class Blog
{
    public int Id { get; set; }

    public string? Title { get; set; }
}

/// <summary>
///     A minimal context usable on both client (InfoCarrier) and server (InMemory) sides.
/// </summary>
public class SmokeContext : DbContext
{
    public SmokeContext(DbContextOptions<SmokeContext> options)
        : base(options)
    {
    }

    public DbSet<Blog> Blogs => Set<Blog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Keys are assigned explicitly (and by the server in the real pipeline) — disable
        // client-side store-generation so the InfoCarrier client doesn't try to generate them.
        modelBuilder.Entity<Blog>().Property(b => b.Id).ValueGeneratedNever();
    }
}
