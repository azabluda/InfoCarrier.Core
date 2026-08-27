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

    public List<Post> Posts { get; set; } = [];
}

/// <summary>
///     A dependent, so SaveChanges can be tested with a principal whose key the store generates
///     — the case where the client's foreign key is temporary and cannot travel.
/// </summary>
public class Post
{
    public int Id { get; set; }

    public string? Heading { get; set; }

    public int BlogId { get; set; }

    public Blog? Blog { get; set; }

    public List<Tag> Tags { get; set; } = [];
}

/// <summary>
///     The other end of a many-to-many. ADR-004 requires M2M from day one — it is v1's stated
///     worst failure mode — and its join entity is what makes SaveChanges hard: a shared-type
///     entity with two foreign keys and no navigations of its own.
/// </summary>
public class Tag
{
    public int Id { get; set; }

    public string? Label { get; set; }

    public List<Post> Posts { get; set; } = [];
}

/// <summary>
///     A minimal context usable on both client (InfoCarrier) and server (InMemory) sides.
/// </summary>
public class SmokeContext(DbContextOptions<SmokeContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs => Set<Blog>();

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<Tag> Tags => Set<Tag>();
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

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<GuidKeyed> GuidKeyed => Set<GuidKeyed>();
}

/// <summary>
///     An entity with a <see cref="System.Guid" /> key, for
///     <c>ServerParameterizationTest</c> (issue #59).
/// </summary>
/// <remarks>
///     The key type is the whole point and an <see cref="int" /> will not do.
///     <c>ExpressionExtensions.BuildPredicate</c> compares a <em>numeric</em> key through
///     <c>EF.Property&lt;TKey&gt;</c>, and every other key type — a <c>Guid</c> above all —
///     through <c>EF.Property&lt;object&gt;</c>. Only the second shape produces the
///     <c>object</c>-typed parameter this exists to pin.
/// </remarks>
public class GuidKeyed
{
    public Guid Id { get; set; }

    public string? Label { get; set; }
}
