// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     A blog, for ADR-009 Tier C's vertical slice.
/// </summary>
/// <remarks>
///     Its own type rather than <see cref="Blog" />, and the model is deliberately tiny. This
///     context exists to prove one thing per test against a store nothing else in the suite has
///     used yet, so every entity it carries has to earn its place.
/// </remarks>
public class FbBlog
{
    /// <summary>
    ///     The key. Assigned by the test, never by the store.
    /// </summary>
    /// <remarks>
    ///     <b>Value generation is off on purpose.</b> Firebird has no identity column in the sense
    ///     SQLite and SQL Server have one, so EF's Firebird provider expects a fixture to say how
    ///     keys are generated. A smoke test is the wrong place to answer that: it would put a
    ///     second thing under test in every assertion.
    /// </remarks>
    public int Id { get; set; }

    /// <summary>
    ///     The blog's title.
    /// </summary>
    public string? Title { get; set; }
}

/// <summary>
///     A post belonging to a <see cref="FbBlog" />.
/// </summary>
public class FbPost
{
    /// <summary>
    ///     The key. Assigned by the test.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The post's heading.
    /// </summary>
    public string? Heading { get; set; }

    /// <summary>
    ///     The owning blog's key.
    /// </summary>
    public int BlogId { get; set; }
}

/// <summary>
///     One row of the table-valued function's result.
/// </summary>
/// <remarks>
///     Keyless and mapped to no table, which is what EF requires of a function's result type. The
///     property names are the procedure's output parameter names, because that is what the reader
///     binds against.
/// </remarks>
public class FbPostRow
{
    /// <summary>
    ///     The post's key.
    /// </summary>
    public int PostId { get; set; }

    /// <summary>
    ///     The post's heading.
    /// </summary>
    public string? Heading { get; set; }
}

/// <summary>
///     The Tier C smoke context: the smallest model that can ask Firebird for the two things
///     SQLite cannot do.
/// </summary>
/// <remarks>
///     <para>
///         <b>A context type of its own, as every tier's smoke context has.</b> EF's test model
///         source caches by context type, so sharing one between two backing stores lets a model
///         built for one provider reach the other. That produced "no such table" from an InMemory
///         provider once, which is a message no InMemory provider can produce.
///     </para>
/// </remarks>
public class FirebirdSmokeContext(DbContextOptions<FirebirdSmokeContext> options) : DbContext(options)
{
    /// <summary>
    ///     The blogs.
    /// </summary>
    public DbSet<FbBlog> Blogs => this.Set<FbBlog>();

    /// <summary>
    ///     The posts.
    /// </summary>
    public DbSet<FbPost> Posts => this.Set<FbPost>();

    /// <summary>
    ///     A <em>scalar</em> store function: how many posts a blog has.
    /// </summary>
    /// <remarks>
    ///     <b>It throws, and that is the assertion.</b> If the client ever evaluates this rather
    ///     than sending it, the failure arrives named instead of passing as a plausible number.
    ///     The same reasoning as <c>SqliteSmokeContext.TitleIsLong</c>.
    /// </remarks>
    public static int PostCountOf(int blogId)
        => throw new NotSupportedException($"{nameof(PostCountOf)} must never run on the client.");

    /// <summary>
    ///     A <em>table-valued</em> store function: the posts of one blog.
    /// </summary>
    /// <remarks>
    ///     <b>This is the whole reason Tier C exists.</b> SQLite cannot express it: its managed
    ///     driver registers scalar delegates and has no virtual-table module, so there is nothing
    ///     for a query root over a function to bind to. Firebird answers it with a selectable
    ///     stored procedure, which is queried exactly as a table-valued function is.
    /// </remarks>
    public IQueryable<FbPostRow> PostsOf(int blogId)
        => this.FromExpression(() => this.PostsOf(blogId));

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<FbBlog>().Property(e => e.Id).ValueGeneratedNever();
        modelBuilder.Entity<FbPost>().Property(e => e.Id).ValueGeneratedNever();

        // Mapped to no table: the rows come from the function and nowhere else.
        modelBuilder.Entity<FbPostRow>().HasNoKey().ToTable((string?)null);

        modelBuilder.HasDbFunction(typeof(FirebirdSmokeContext).GetMethod(nameof(PostCountOf))!);
        modelBuilder.HasDbFunction(typeof(FirebirdSmokeContext).GetMethod(nameof(PostsOf))!);
    }
}
