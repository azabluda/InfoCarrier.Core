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
///     An entity with a member the CLIENT model does not map and the SERVER model does.
/// </summary>
/// <remarks>
///     <para>
///         <b>The only place in this repository where the two models disagree about what is
///         mapped</b>, and it exists to pin what happens then. <c>SqliteSmokeContext</c> ignores
///         <see cref="Note" />, so the client's model has no such property; the store's own model
///         customizer maps it, so the server's has one and the table has the column. Every other
///         fixture builds both models from one <c>OnModelCreating</c>, which is why nothing else
///         can exercise this.
///     </para>
///     <para>
///         The shape is EF's own: the core <c>NorthwindContext</c> ignores ten <c>Order</c>
///         properties that the real Northwind schema has, and R138 found this defect by mapping
///         them on the server and watching four unmapped-property spec tests stop throwing.
///     </para>
/// </remarks>
public class Shipment
{
    public int Id { get; set; }

    /// <summary>Mapped on both sides, so a query that touches only this one is ordinary.</summary>
    public string? Reference { get; set; }

    /// <summary>Mapped on the server alone. The client's model does not know it exists.</summary>
    public string? Note { get; set; }
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

    public DbSet<Located> Located => Set<Located>();

    public DbSet<Shipment> Shipments => Set<Shipment>();

    /// <summary>
    ///     A store function mapped as an <em>instance</em> method on the context, for R89's pin.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Instance, and mapped, are both load-bearing.</b> R84 admits the declaring type
    ///         of every <c>HasDbFunction</c> mapping to the allowlist so that the call can be
    ///         <em>named</em> on the wire; for an instance mapping that type is this context. The
    ///         pin is that being nameable did not make the call shippable — its <c>Object</c> is
    ///         the live client context — and that it is therefore still refused.
    ///     </para>
    ///     <para>
    ///         <b>It throws on purpose.</b> The failure guarded against is the client fetching the
    ///         whole table and running this locally. A body returning a plausible answer would let
    ///         that regression pass as a green count; a body that throws makes it arrive named, in
    ///         the assertion message.
    ///     </para>
    /// </remarks>
    public bool TitleIsLong(string? title)
        => throw new NotSupportedException($"{nameof(TitleIsLong)} must never run on the client.");

    /// <summary>
    ///     The same thing declared by <b>attribute</b> rather than by <c>HasDbFunction</c>, for
    ///     R91's probe of D7's <c>RelationalDbFunctionAttributeConvention</c> row.
    /// </summary>
    /// <remarks>
    ///     That convention is relational, so only the server runs it. The probe asks whether the
    ///     two models therefore disagree about which methods this model maps.
    /// </remarks>
    [DbFunction]
    public static bool TitleIsShort(string? title)
        => throw new NotSupportedException($"{nameof(TitleIsShort)} must never run on the client.");

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // R89's pin. Mapping it is what puts this context type on the allowlist, which is the
        // condition the pin is about; no test ever runs the function, so the store needs no
        // definition for it.
        modelBuilder.HasDbFunction(typeof(SqliteSmokeContext).GetMethod(nameof(TitleIsLong))!);

        modelBuilder.Entity<StructKeyed>()
            .Property(e => e.Id)
            .HasConversion(k => k.Value, v => new IntStructKey(v));

        modelBuilder.Entity<Addressed>().ComplexProperty(e => e.Address);

        // THE CLIENT MODEL DOES NOT MAP THIS AND THE SERVER MODEL DOES. This context builds both,
        // so the ignore reaches both; `UnmappedMemberBoundaryTest` hands the store a model
        // customizer that maps it again on the server alone. See `Shipment`.
        modelBuilder.Entity<Shipment>().Ignore(e => e.Note);

        // An OWNED type one of whose values has no public CLR member -- it lives in a private
        // field reached through an indexer. That is the shape R64 turns on: a value the wire can
        // carry only if it knows the entity type, so a projection whose shape went unresolved
        // dropped it silently while the public members beside it survived.
        modelBuilder.Entity<Located>().OwnsOne(e => e.Address, b => b.IndexerProperty<string>("Line"));

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

/// <summary>
///     An entity with an owned address, for R64's <c>LeftJoin</c> pin.
/// </summary>
/// <remarks>
///     Modelled on <c>OwnedQueryTestBase.OwnedAddress</c>, which is where the defect was found:
///     one value behind an indexer and one an ordinary property, so a wire that falls back to
///     public CLR members loses exactly half of it and the loss is visible in one assertion.
/// </remarks>
public class Located
{
    public int Id { get; set; }

    public LocatedAddress Address { get; set; } = new();
}

/// <summary>
///     An owned address whose <c>Line</c> has no public CLR member.
/// </summary>
public class LocatedAddress
{
    private string? _line;

    public string? City { get; set; }

    public object? this[string name]
    {
        get => name == "Line"
            ? _line
            : throw new InvalidOperationException($"Indexer property with key {name} is not defined on {nameof(LocatedAddress)}.");

        set
        {
            if (name != "Line")
            {
                throw new InvalidOperationException(
                    $"Indexer property with key {name} is not defined on {nameof(LocatedAddress)}.");
            }

            _line = (string?)value;
        }
    }
}

/// <summary>
///     The principal half of a table-split pair, carrying the concurrency token.
/// </summary>
public class SharedRoot
{
    public int Id { get; set; }

    public string? Name { get; set; }

    /// <summary>
    ///     An application-managed concurrency token — the shape SQLite supports, since it has no
    ///     native <c>rowversion</c>.
    /// </summary>
    public string? Version { get; set; }

    public SharedDetail? Detail { get; set; }
}

/// <summary>
///     The dependent half, which shares <see cref="SharedRoot" />'s table and has no token of
///     its own. That is the shape <c>TableSharingConcurrencyTokenConvention</c> exists for.
/// </summary>
public class SharedDetail
{
    public int Id { get; set; }

    public string? Note { get; set; }

    public SharedRoot? Root { get; set; }
}

/// <summary>
///     Two entity types split across one table, one of them carrying a concurrency token.
/// </summary>
/// <remarks>
///     <para>
///         <b>A probe for D7's <c>TableSharingConcurrencyTokenConvention</c> row, and a context of
///         its own so the model change reaches nothing else.</b> That convention is relational:
///         where entity types share a table and only some carry a concurrency token, it gives the
///         others a <em>shadow</em> token property named
///         <c>_TableSharingConcurrencyTokenConvention_&lt;name&gt;</c> mapped to the same column.
///         This client does not run it, so the server's model has a property the client's does
///         not — and the property set is what <c>SaveChanges</c> sends.
///     </para>
///     <para>
///         <b>EF ships no store-agnostic test for it.</b> Its only functional coverage is
///         <c>OptimisticConcurrencySqlServerTest</c>, which is SQL Server's own class rather than
///         a specification base and uses <c>rowversion</c>; the rest is a unit test in
///         <c>EFCore.Relational.Tests</c>. So nothing this repository inherits can reach it, and
///         a probe is the only way to know.
///     </para>
/// </remarks>
public class TableSplitSmokeContext(DbContextOptions<TableSplitSmokeContext> options) : DbContext(options)
{
    public DbSet<SharedRoot> Roots => Set<SharedRoot>();

    public DbSet<SharedDetail> Details => Set<SharedDetail>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<SharedRoot>(b =>
        {
            b.ToTable("Shared");
            // BOTH clauses are required and only one is obvious. `FindConcurrencyColumns` skips
            // a token that is not also `ValueGenerated.OnUpdate`, so an application-managed token
            // -- the only kind SQLite has -- never reaches the convention at all.
            b.Property(e => e.Version).IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
            b.HasOne(e => e.Detail).WithOne(e => e.Root).HasForeignKey<SharedDetail>(e => e.Id);
        });

        modelBuilder.Entity<SharedDetail>().ToTable("Shared");
    }
}
