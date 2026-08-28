// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     Asserts that a query which crosses the wire reaches the backing store as the <em>same
///     statement</em> as the one written directly against the server context (issue #59).
/// </summary>
/// <remarks>
///     <para>
///         <b>This is a differential test and deliberately not a baseline one.</b> EF's relational
///         spec bases pin generated SQL against golden strings, which here would pin SQLite's
///         dialect — the backend's business, tested by EF, and not observable from this client.
///         The question this repository has to answer is different: <em>does the middleman between
///         the caller's LINQ and the store's SQL change the statement?</em> Running the same query
///         both ways and comparing answers that question without a single golden string, and it
///         survives an EF version bump.
///     </para>
///     <para>
///         <b>Parameter names are normalized before the comparison, and that is not laziness.</b>
///         A parameter reaches the server inside a <c>ParameterBox&lt;T&gt;</c>, so EF names it
///         after the box's property and the caller's local variable name never crosses the wire.
///         <c>@Value</c> against <c>@title</c> is the expected difference; <c>'beta'</c> against
///         <c>@title</c> is the defect.
///     </para>
///     <para>
///         The SQL is captured from the <b>server</b> context, through the
///         <see cref="SharedTestStoreProperties.OnAddOptions" /> hook. Nothing else in this suite
///         looks at server SQL: <c>InfoCarrierTestStoreFactory</c>'s <c>TestSqlLoggerFactory</c>
///         belongs to the client, which has no database and emits none.
///     </para>
/// </remarks>
public partial class ServerParameterizationTest
{
    private readonly List<string> _sink = [];

    [ConditionalFact]
    public Task A_scalar_string_parameter_stays_a_parameter()
        => AssertSameStatement(
            "beta",
            static (blogs, title) => blogs.Where(b => b.Title == title));

    [ConditionalFact]
    public Task A_scalar_int_parameter_stays_a_parameter()
        => AssertSameStatement(
            2,
            static (blogs, minId) => blogs.Where(b => b.Id >= minId));

    [ConditionalFact]
    public Task A_collection_parameter_stays_an_IN_list()
        => AssertSameStatement(
            new List<string> { "alpha", "gamma" },
            static (blogs, titles) => blogs.Where(b => titles.Contains(b.Title!)));

    [ConditionalFact]
    public Task A_limit_and_offset_stay_parameters()
        => AssertSameStatement(
            2,
            static (blogs, take) => blogs.OrderBy(b => b.Id).Skip(1).Take(take));

    /// <summary>
    ///     A key lookup on a <see cref="Guid" /> key, which is the case a whole-suite sweep found
    ///     after the scalar one was fixed.
    /// </summary>
    /// <remarks>
    ///     <c>ExpressionExtensions.BuildPredicate</c> builds this as
    ///     <c>EF.Property&lt;object&gt;(e, "Id") == keyValues[i]</c>, so the parameter's declared
    ///     type is <c>object</c> and the guard that excluded <c>object</c> excluded every
    ///     non-numeric key lookup with it — 3,927 of them in the SQLite tier alone, each reaching
    ///     the store as a literal. `Find` is not an edge case.
    /// </remarks>
    [ConditionalFact]
    public async Task A_Guid_key_lookup_stays_a_parameter()
    {
        Guid id = new("11111111-1111-1111-1111-111111111111");

        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new GuidKeyed { Id = id, Label = "one" });
                await context.SaveChangesAsync();
            });

        Drain();

        await using (SqliteSmokeContext client = new(
            new DbContextOptionsBuilder<SqliteSmokeContext>().UseInfoCarrier(store).Options))
        {
            Assert.NotNull(await client.GuidKeyed.FindAsync(id));
        }

        string overTheWire = SingleStatement(Drain());

        using (DbContext server = store.CreateDbContext())
        {
            Assert.NotNull(await server.Set<GuidKeyed>().FindAsync(id));
        }

        Assert.Equal(SingleStatement(Drain()), overTheWire);
    }

    /// <summary>
    ///     Four cases where a <em>constant</em> and a <em>parameter</em> do not merely differ in
    ///     the literal: they make EF translate the query differently.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The cases above all began as an inlined value that should have been a parameter, and
    ///         each was found by counting substitutions. These four come from the other direction:
    ///         ask where the wire could plausibly change the <em>shape</em> of the statement, and
    ///         pin those places whether or not a defect is there today.
    ///     </para>
    ///     <para>
    ///         A null, a <c>StartsWith</c> argument, an empty <c>Contains</c> list and a value in
    ///         the projection are the four EF handles specially. A null compiles to <c>IS NULL</c>
    ///         when EF can see it and to a null-semantics expansion when it cannot; a
    ///         <c>StartsWith</c> constant can become a plain <c>LIKE 'x%'</c> where a parameter
    ///         cannot; an empty list is a constant-folded predicate; and a projected value is the
    ///         one place a parameter appears outside a predicate.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public Task A_null_string_parameter_matches_the_direct_query()
        => AssertSameStatement<string?, Blog>(
            null,
            static (blogs, title) => blogs.Where(b => b.Title == title));

    /// <inheritdoc cref="A_null_string_parameter_matches_the_direct_query" />
    [ConditionalFact]
    public Task A_StartsWith_parameter_matches_the_direct_query()
        => AssertSameStatement<string, Blog>(
            "al",
            static (blogs, prefix) => blogs.Where(b => b.Title!.StartsWith(prefix)));

    /// <inheritdoc cref="A_null_string_parameter_matches_the_direct_query" />
    [ConditionalFact]
    public Task An_empty_collection_parameter_matches_the_direct_query()
        => AssertSameStatement<List<string>, Blog>(
            [],
            static (blogs, titles) => blogs.Where(b => titles.Contains(b.Title!)));

    /// <inheritdoc cref="A_null_string_parameter_matches_the_direct_query" />
    [ConditionalFact]
    public Task A_parameter_in_the_projection_matches_the_direct_query()
        => AssertSameStatement<string, string>(
            "!",
            static (blogs, suffix) => blogs.Select(b => b.Title + suffix));

    /// <summary>
    ///     Four more, where the parameter sits inside a construct that changes the statement's
    ///     structure rather than one of its comparisons.
    /// </summary>
    /// <remarks>
    ///     An <c>Include</c> is a join, a <c>GroupBy</c> is a <c>GROUP BY</c>, an <c>Any</c> over a
    ///     navigation is a correlated <c>EXISTS</c>, and a nullable value type is the shape J19's
    ///     null rule is about. Each is a place where a middleman that mishandled the parameter
    ///     would show up as a different statement rather than a different literal.
    /// </remarks>
    [ConditionalFact]
    public Task A_parameter_under_Include_matches_the_direct_query()
        => AssertSameStatement<string, Blog>(
            "beta",
            static (blogs, title) => blogs.Include(b => b.Posts).Where(b => b.Title == title));

    /// <inheritdoc cref="A_parameter_under_Include_matches_the_direct_query" />
    [ConditionalFact]
    public Task A_parameter_under_GroupBy_matches_the_direct_query()
        => AssertSameStatement<int, string?>(
            2,
            static (blogs, minId) => blogs.Where(b => b.Id >= minId).GroupBy(b => b.Title).Select(g => g.Key));

    /// <inheritdoc cref="A_parameter_under_Include_matches_the_direct_query" />
    [ConditionalFact]
    public Task A_parameter_inside_Any_matches_the_direct_query()
        => AssertSameStatement<string, Blog>(
            "first",
            static (blogs, heading) => blogs.Where(b => b.Posts.Any(p => p.Heading == heading)));

    /// <inheritdoc cref="A_parameter_under_Include_matches_the_direct_query" />
    [ConditionalFact]
    public Task A_nullable_value_type_parameter_matches_the_direct_query()
        => AssertSameStatement<int?, Blog>(
            2,
            static (blogs, id) => blogs.Where(b => b.Id == id));

    /// <summary>
    ///     A collection the box cannot hand back, category 3 of issue #62.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Substitute</c> boxes a collection only where the parameter's declared type can
    ///         accept the <c>List&lt;T&gt;</c> the far side rebuilds. A <c>HashSet&lt;T&gt;</c>
    ///         cannot, so the value is spelled out as a constant instead. The question these three
    ///         cases answer is whether that reaches the store as a literal list or as parameters,
    ///         and the only way to know is to read the statement.
    ///     </para>
    ///     <para>
    ///         <c>IOrderedEnumerable&lt;T&gt;</c> is deliberately absent. Boxing it broke eight
    ///         <c>Contains_with_local_ordered_enumerable_*</c> tests, and that carve-out is
    ///         documented in <c>Substitute</c> itself.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public Task A_HashSet_parameter_matches_the_direct_query()
        => AssertSameStatement(
            new HashSet<string> { "alpha", "gamma" },
            static (blogs, titles) => blogs.Where(b => titles.Contains(b.Title!)));

    /// <inheritdoc cref="A_HashSet_parameter_matches_the_direct_query" />
    [ConditionalFact]
    public Task An_ImmutableArray_parameter_matches_the_direct_query()
        => AssertSameStatement(
            ImmutableArray.Create("alpha", "gamma"),
            static (blogs, titles) => blogs.Where(b => titles.Contains(b.Title!)));

    /// <inheritdoc cref="A_HashSet_parameter_matches_the_direct_query" />
    [ConditionalFact]
    public Task A_ReadOnlyCollection_parameter_matches_the_direct_query()
        => AssertSameStatement(
            new ReadOnlyCollection<string>(["alpha", "gamma"]),
            static (blogs, titles) => blogs.Where(b => titles.Contains(b.Title!)));

    /// <summary>
    ///     An entity compared as a whole, category 2 of issue #62.
    /// </summary>
    /// <remarks>
    ///     EF expands <c>b == blog</c> into a comparison of the key, so what reaches the store is a
    ///     key value and not an entity. The open question is whether that key lands as a parameter
    ///     here as it does on EF's own client, or as a literal: <c>Substitute</c> excludes an
    ///     entity-typed parameter from boxing on the grounds that EF expands it itself.
    /// </remarks>
    [ConditionalFact]
    public Task An_entity_constant_matches_the_direct_query()
        => AssertSameStatement(
            new Blog { Id = 2, Title = "beta" },
            static (blogs, blog) => blogs.Where(b => b == blog));

    /// <summary>
    ///     A key lookup on a struct key behind a value converter, category 1 of issue #62.
    /// </summary>
    /// <remarks>
    ///     The declared parameter type is <c>object</c>, as it is for every non-numeric key, and
    ///     <c>Substitute</c> boxes one of those only when its runtime type is a wire primitive. An
    ///     <c>IntStructKey</c> is not, so the key is inlined. Boxing on the declared type alone was
    ///     tried during #59 and broke 21 <c>KeysWithConvertersInfoCarrierTest</c> tests with
    ///     "Object must implement IConvertible"; the open question is whether the value can cross
    ///     in its <em>converted</em> form instead.
    /// </remarks>
    [ConditionalFact]
    public async Task A_converted_struct_key_lookup_matches_the_direct_query()
    {
        IntStructKey id = new(7);

        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new StructKeyed { Id = id, Label = "one" });
                await context.SaveChangesAsync();
            });

        Drain();

        await using (SqliteSmokeContext client = new(
            new DbContextOptionsBuilder<SqliteSmokeContext>().UseInfoCarrier(store).Options))
        {
            Assert.NotNull(await client.StructKeyed.FindAsync(id));
        }

        string overTheWire = SingleStatement(Drain());

        using (DbContext server = store.CreateDbContext())
        {
            Assert.NotNull(await server.Set<StructKeyed>().FindAsync(id));
        }

        Assert.Equal(SingleStatement(Drain()), overTheWire);
    }

    /// <summary>
    ///     A complex type compared as a whole, category 4 of issue #62.
    /// </summary>
    /// <remarks>
    ///     EF splits a complex value into one parameter per property. The question is whether this
    ///     side sends one constant instead, which would reach the store as literals.
    /// </remarks>
    [ConditionalFact]
    public async Task A_complex_type_value_matches_the_direct_query()
    {
        Address address = new() { City = "Oslo", Postcode = "0150" };

        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Addressed { Id = 1, Address = new Address { City = "Oslo", Postcode = "0150" } });
                await context.SaveChangesAsync();
            });

        Drain();

        await using (SqliteSmokeContext client = new(
            new DbContextOptionsBuilder<SqliteSmokeContext>().UseInfoCarrier(store).Options))
        {
            _ = await client.Addressed.Where(e => e.Address == address).ToListAsync();
        }

        string overTheWire = SingleStatement(Drain());

        using (DbContext server = store.CreateDbContext())
        {
            _ = await server.Set<Addressed>().Where(e => e.Address == address).ToListAsync();
        }

        Assert.Equal(SingleStatement(Drain()), overTheWire);
    }

    /// <summary>
    ///     Runs <paramref name="query" /> over the wire and again directly against the server, and
    ///     asserts the store saw one statement, not two.
    /// </summary>
    private async Task AssertSameStatement<TValue, TResult>(
        TValue value,
        Func<IQueryable<Blog>, TValue, IQueryable<TResult>> query)
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(
                    new Blog { Id = 1, Title = "alpha" },
                    new Blog { Id = 2, Title = "beta" },
                    new Blog { Id = 3, Title = "gamma" });
                await context.SaveChangesAsync();
            });

        Drain();

        await using (SqliteSmokeContext client = new(
            new DbContextOptionsBuilder<SqliteSmokeContext>().UseInfoCarrier(store).Options))
        {
            _ = await query(client.Blogs, value).ToListAsync();
        }

        string overTheWire = SingleStatement(Drain());

        using (DbContext server = store.CreateDbContext())
        {
            _ = await query(server.Set<Blog>(), value).ToListAsync();
        }

        string directly = SingleStatement(Drain());

        Assert.Equal(directly, overTheWire);
    }

    private SqliteInfoCarrierBackendTestStore CreateStore()
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SqliteSmokeContext),
                OnModelCreating = (_, _) => { },
                OnAddOptions = b => b.LogTo(
                    line => { lock (_sink) { _sink.Add(line); } },
                    [RelationalEventId.CommandExecuted]),
            });

    private string[] Drain()
    {
        lock (_sink)
        {
            string[] copy = [.. _sink];
            _sink.Clear();
            return copy;
        }
    }

    /// <summary>
    ///     The one statement in <paramref name="logged" />, with parameter names normalized and
    ///     the timing preamble dropped.
    /// </summary>
    private static string SingleStatement(string[] logged)
    {
        string entry = Assert.Single(logged);

        // The first two lines are the event header and the `Executed DbCommand (0ms) [...]`
        // preamble, whose elapsed time and parameter *names* both vary. The statement follows.
        string[] lines = entry.Split('\n');
        int start = Array.FindIndex(lines, l => l.TrimStart().StartsWith("SELECT", StringComparison.Ordinal));
        Assert.True(start >= 0, "no SELECT found in: " + entry);

        string sql = string.Join('\n', lines[start..]).Trim();

        return ParameterName().Replace(sql, "@p");
    }

    [GeneratedRegex(@"@[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex ParameterName();
}
