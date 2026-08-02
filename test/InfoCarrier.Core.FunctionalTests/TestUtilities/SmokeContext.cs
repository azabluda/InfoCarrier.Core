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
}

/// <summary>
///     The same shape as <see cref="SmokeContext" />, for the SQLite tier.
/// </summary>
/// <remarks>
///     A distinct context <em>type</em> on purpose. EF's test model source caches by context
///     type, so sharing one between an InMemory-backed and a SQLite-backed store let a model
///     built for one provider reach the other — the InMemory smoke test failed with
///     "no such table: Blogs", which no InMemory provider can produce.
/// </remarks>
public class SqliteSmokeContext(DbContextOptions<SqliteSmokeContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs => Set<Blog>();
}
