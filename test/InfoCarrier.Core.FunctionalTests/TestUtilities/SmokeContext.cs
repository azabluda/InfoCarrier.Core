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

    public DbSet<StructKeyed> StructKeyed => Set<StructKeyed>();

    public DbSet<Addressed> Addressed => Set<Addressed>();

    public DbSet<Coded> Coded => Set<Coded>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<StructKeyed>()
            .Property(e => e.Id)
            .HasConversion(k => k.Value, v => new IntStructKey(v));

        modelBuilder.Entity<Addressed>().ComplexProperty(e => e.Address);

        // An alternate key, which SQLite renders as a UNIQUE table constraint. It is the only
        // thing in this model that reaches CommandBatchPreparer.AddUniqueValueEdges' unique-
        // constraint branch, and R44 exists to measure that branch over the wire.
        modelBuilder.Entity<Coded>(b =>
        {
            b.Property(e => e.Code).IsRequired();
            b.HasAlternateKey(e => e.Code);
        });
    }
}

/// <summary>
///     An entity with an alternate key, for the R44 originals audit.
/// </summary>
/// <remarks>
///     <para>
///         <c>CommandBatchPreparer.AddUniqueValueEdges</c> orders a <c>Deleted</c> command before
///         an <c>Added</c> one that reuses the same <em>unique constraint</em> value, and it reads
///         the deleted row's value <c>fromOriginalValues: true</c>. A table's unique constraints
///         are its primary key and its alternate keys, and nothing else in this model has an
///         alternate key.
///     </para>
///     <para>
///         The audit's answer is that the wire cannot lose that original, because <c>Code</c> is a
///         key property and EF fixes every key property's <c>AfterSaveBehavior</c> at
///         <c>Throw</c> — <c>Property.CheckAfterSaveBehavior</c> refuses to configure any other
///         value, so a saved key can never be changed and its original can never differ from its
///         current. This entity is what lets the scenario be run at all.
///     </para>
/// </remarks>
public class Coded
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Label { get; set; }
}

/// <summary>
///     An entity whose key is a struct behind a value converter, for <c>ServerParameterizationTest</c>
///     (issue #62, category 1).
/// </summary>
/// <remarks>
///     The converter is the point. <c>ExpressionExtensions.BuildPredicate</c> compares a
///     non-numeric key through <c>EF.Property&lt;object&gt;</c>, and <c>Substitute</c> boxes an
///     <c>object</c>-typed parameter only when its <em>runtime</em> type is a wire primitive. An
///     <see cref="IntStructKey" /> is not one, so <c>Find</c> on such a key still sends a literal.
///     Whether the value can cross in its <em>converted</em> form is what #62 asks.
/// </remarks>
public class StructKeyed
{
    public IntStructKey Id { get; set; }

    public string? Label { get; set; }
}

/// <summary>
///     A struct key, converted to <see cref="int" /> by the model.
/// </summary>
/// <param name="Value">The underlying value.</param>
public readonly record struct IntStructKey(int Value);

/// <summary>
///     An entity with a complex property, for <c>ServerParameterizationTest</c> (issue #62,
///     category 4). EF splits a complex value into one parameter per property; the question is
///     whether this side sends one constant instead.
/// </summary>
public class Addressed
{
    public int Id { get; set; }

    public Address Address { get; set; } = new();
}

/// <summary>
///     A complex type, compared as a whole by the test.
/// </summary>
public class Address
{
    public string? City { get; set; }

    public string? Postcode { get; set; }
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
