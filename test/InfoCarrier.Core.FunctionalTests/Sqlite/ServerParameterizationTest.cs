// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
///         <c>@title</c> is a defect, and so is <c>@Value</c> where the direct query has
///         <c>'beta'</c>. Either direction changes the plan the caller asked for (the owner,
///         2026-09-15): a literal is parsed again for every value, and a parameter hides the value
///         from the optimizer.
///     </para>
///     <para>
///         The SQL is captured from the <b>server</b> context, through the
///         <see cref="SharedTestStoreProperties.OnAddOptions" /> hook. Nothing else in this suite
///         looks at server SQL: <c>InfoCarrierTestStoreFactory</c>'s <c>TestSqlLoggerFactory</c>
///         belongs to the client, which has no database and emits none.
///     </para>
///     <para>
///         <b>A PASS HERE IS NOT EVIDENCE ABOUT WHAT THE SERVER READ, and every statement in this
///         class was read on 2026-09-23 because of it</b> (the owner, 2026-09-22). A differential
///         test says "the same statement as plain EF" and stays silent when both sides read
///         everything, so the green of a test whose name promises a narrow query proves only that
///         the middleman changed nothing. The tests added on 2026-09-22 had their SQL read as they
///         were written; the 34 older ones had not, and the reading found nothing wrong: every one
///         carries the predicate, limit or join its name is about. The three statements with no
///         predicate are the query's own — a projection over every blog, a <c>GroupJoin</c> whose
///         result selector discards the group so EF itself writes <c>SELECT "Id" FROM "Blogs"</c>,
///         and the <c>An_unregistered_…</c> family, whose names say they read the table and whose
///         assertions already say so.
///     </para>
///     <para>
///         <b>That reading needed a fix first.</b> This class was invisible to
///         <c>INFOCARRIER_SERVER_SQL</c>, so <c>eng/ef-sql-diff.py</c> could not see the one class
///         whose whole subject is the statement: its <c>LogTo</c> in <c>CreateStore</c> replaced
///         the store's. This paragraph said the fix was "the one in <c>CreateStore</c>" from
///         2026-09-23, when <c>CreateStore</c> forwarded each line to the log itself. Since
///         2026-09-24 the store writes the log through <see cref="ServerSqlLogInterceptor" />,
///         which no fixture's <c>LogTo</c> can displace, and the forward is gone.
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

    /// <summary>
    ///     A list filtered by the row it is compared with keeps the collection mode the server is
    ///     configured with.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The client used to override that mode, whatever either half was configured
    ///         with.</b> <c>Where</c> transforms a collection parameter, so the client wrapped
    ///         <c>ids</c> in <c>EF.MultipleParameters</c>, and EF gives a mode on one parameter
    ///         priority over the server's option. A server set to <c>Constant</c> then ran
    ///         <c>VALUES (@p), (@p)</c> where EF runs <c>VALUES (1), (3)</c>, and one set to
    ///         <c>Parameter</c> got a statement whose text changes with the size of the list.
    ///         Measured 2026-09-21, here and in <c>AdHocMiscellaneous.Check_inlined_constants_redacting</c>.
    ///     </para>
    ///     <para>
    ///         The wrapper is for an operator the server could otherwise evaluate, which is a
    ///         compiled query's. This one reads the row, so nothing can evaluate it, and EF's own
    ///         client leaves the mode to the server. <c>MultipleParameters</c> is EF's default and
    ///         passed before the fix, which makes it the control.
    ///     </para>
    /// </remarks>
    [ConditionalTheory]
    [InlineData(ParameterTranslationMode.Constant)]
    [InlineData(ParameterTranslationMode.Parameter)]
    [InlineData(ParameterTranslationMode.MultipleParameters)]
    public Task A_list_filtered_by_the_row_keeps_the_servers_collection_mode(ParameterTranslationMode mode)
        => AssertSameStatement(
            new List<int> { 1, 3 },
            static (blogs, ids) => blogs.Where(b => ids.Where(i => i == b.Id).Any()),
            mode);

    /// <summary>
    ///     A compiled query's list, transformed before it is compared, keeps the collection mode the
    ///     server is configured with.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The client marks such a list so that the server does not evaluate the operator over
    ///         it</b> (<see cref="ServerSqlTest.A_compiled_query_keeps_its_collection_a_parameter" />),
    ///         and the mark is <c>EF.MultipleParameters</c>, which names EF's default mode. EF prefers
    ///         a mode on one parameter to the server's option, so a server set to <c>Constant</c> ran
    ///         <c>SELECT 0, @p UNION ALL VALUES (1, @p)</c> where EF runs
    ///         <c>SELECT 0, CAST(1 AS INTEGER) UNION ALL VALUES (1, 2)</c>, and one set to
    ///         <c>Parameter</c> ran the same where EF runs <c>json_each(@p)</c>. Measured 2026-09-21.
    ///     </para>
    ///     <para>
    ///         The server now replaces the mark with EF's marker for the mode it is configured with.
    ///         <c>MultipleParameters</c> passed before the change and is the control.
    ///     </para>
    /// </remarks>
    [ConditionalTheory]
    [InlineData(ParameterTranslationMode.Constant)]
    [InlineData(ParameterTranslationMode.Parameter)]
    [InlineData(ParameterTranslationMode.MultipleParameters)]
    public Task A_compiled_query_transforming_a_list_keeps_the_servers_collection_mode(ParameterTranslationMode mode)
    {
        // One compiled query per context: EF binds a compiled query to the first model it runs on,
        // and the client's model is not the server's.
        static Func<DbContext, int[], Task<int>> Compile()
            => EF.CompileAsyncQuery(
                (DbContext context, int[] ids) => context.Set<Blog>().Count(b => ids.Skip(1).Contains(b.Id)));

        return AssertSameStatementFor(context => Compile()(context, [1, 2, 3]), mode);
    }

    /// <summary>
    ///     A compiled query that consumes its list runs plain EF Core's statement.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The client did not mark a list that an operator consumes, until 2026-09-22.</b> The
    ///         server evaluated <c>ids.Count()</c> and ran <c>WHERE "b"."Id" &lt; @p</c>, where plain
    ///         EF Core runs <c>WHERE "b"."Id" &lt; json_array_length(@p)</c>. Neither has a literal or
    ///         changes with the list; the owner chose EF's statement over a difference to explain.
    ///     </para>
    ///     <para>
    ///         Only <c>Parameter</c> mode is here, because it is the only mode in which EF Core 10
    ///         answers; the other two are the next test.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public Task A_compiled_query_consuming_a_list_matches_the_direct_query()
        => AssertSameStatementFor(
            context => CountBelowTheListSize()(context, [1, 2, 3]),
            ParameterTranslationMode.Parameter);

    /// <summary>
    ///     A compiled query that consumes its list fails where plain EF Core 10 fails, and runs no
    ///     statement.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>This is EF's defect, dotnet/efcore#37370, and it is parked until EF Core 11</b> (the
    ///         owner, 2026-09-22). EF has no element type mapping for a list that nothing compares with
    ///         a column, and in these two modes it throws <c>UnreachableException</c>. dotnet/efcore#37372
    ///         fixed it for EF Core 11 only.
    ///     </para>
    ///     <para>
    ///         <b>Until 2026-09-22 this client answered here</b>, because it did not mark the list and
    ///         the server evaluated <c>ids.Count()</c>. A list it did mark, <c>ids.Skip(1).Count()</c>,
    ///         already failed the same way. The owner chose EF's behaviour for both.
    ///     </para>
    ///     <para>
    ///         <b>This turns red when the server runs EF Core 11</b>, because the direct query answers
    ///         then. The modes belong in the test above at that point; <c>docs/plans/v11/</c> records it.
    ///     </para>
    /// </remarks>
    [ConditionalTheory]
    [InlineData(ParameterTranslationMode.Constant)]
    [InlineData(ParameterTranslationMode.MultipleParameters)]
    public async Task A_compiled_query_consuming_a_list_fails_where_EF_Core_10_fails(ParameterTranslationMode mode)
    {
        (Exception overTheWire, string[] wireStatements, Exception directly) =
            await RunFailingBothWays(context => CountBelowTheListSize()(context, [1, 2, 3]), mode);

        Assert.IsType<UnreachableException>(directly);
        Assert.IsType<UnreachableException>(overTheWire);
        Assert.Empty(wireStatements);
    }

    // One compiled query per context: EF binds a compiled query to the first model it runs on, and
    // the client's model is not the server's.
    private static Func<DbContext, int[], Task<int>> CountBelowTheListSize()
        => EF.CompileAsyncQuery(
            (DbContext context, int[] ids) => context.Set<Blog>().Count(b => b.Id < ids.Count()));

    /// <summary>
    ///     A compiled query that reads one element of its list by index runs plain EF Core's statement.
    /// </summary>
    /// <remarks>
    ///     <b>The server evaluated the index until 2026-09-22</b> and ran <c>WHERE "b"."Id" = @p</c>,
    ///     where plain EF Core keeps the list and runs <c>WHERE "b"."Id" = @p -&gt;&gt; 1</c>, in all three
    ///     modes. The same fold answered <c>(string)parameters[0]</c>, which EF refuses:
    ///     <c>PrimitiveCollectionsQuerySqliteInfoCarrierTest</c> now inherits that refusal. The client
    ///     marks the list with <c>EF.Parameter</c> here and not with the mode-named mark, because a
    ///     server in <c>Constant</c> mode then inlined the list as <c>'[1,2,3]' -&gt;&gt; 1</c>.
    /// </remarks>
    [ConditionalTheory]
    [InlineData(ParameterTranslationMode.Constant, false)]
    [InlineData(ParameterTranslationMode.Parameter, false)]
    [InlineData(ParameterTranslationMode.MultipleParameters, false)]
    [InlineData(ParameterTranslationMode.Constant, true)]
    [InlineData(ParameterTranslationMode.Parameter, true)]
    [InlineData(ParameterTranslationMode.MultipleParameters, true)]
    public Task A_compiled_query_indexing_a_list_matches_the_direct_query(ParameterTranslationMode mode, bool list)
    {
        // An array index is a `BinaryExpression` and a list index is a call to `get_Item`, so each
        // is its own path through the client.
        Func<DbContext, Task<int>> run = list
            ? context => EF.CompileAsyncQuery(
                (DbContext c, List<int> ids) => c.Set<Blog>().Count(b => b.Id == ids[1]))(context, [1, 2, 3])
            : context => EF.CompileAsyncQuery(
                (DbContext c, int[] ids) => c.Set<Blog>().Count(b => b.Id == ids[1]))(context, [1, 2, 3]);

        return AssertSameStatementFor(run, mode);
    }

    /// <summary>
    ///     A list the caller marks with <c>EF.MultipleParameters</c> keeps the caller's mode, whatever
    ///     the server is configured with.
    /// </summary>
    /// <remarks>
    ///     The server replaces the client's own mark (above) and must leave the caller's alone. They
    ///     are told apart by what they wrap: the client marks a <c>ParameterBox</c>, and a marker the
    ///     caller wrote reaches the server over a plain constant.
    /// </remarks>
    [ConditionalTheory]
    [InlineData(ParameterTranslationMode.Constant)]
    [InlineData(ParameterTranslationMode.Parameter)]
    public Task A_list_the_caller_marks_keeps_the_callers_mode(ParameterTranslationMode mode)
        => AssertSameStatement(
            new List<int> { 1, 3 },
            static (blogs, ids) => blogs.Where(b => EF.MultipleParameters(ids).Contains(b.Id)),
            mode);

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
    ///     An inline collection the caller writes out of captured values.
    /// </summary>
    /// <remarks>
    ///     EF's own client sends <c>new[] { first, second }.Contains(b.Id)</c> as
    ///     <c>IN (@first, @second)</c>. Until 2026-09-15 each element crossed as a plain constant and
    ///     reached the store as <c>IN (1, 3)</c>, a new statement for every pair of values. A parameter
    ///     replaced by its literal values is a defect, whatever it buys; <c>Substitute</c> records
    ///     what it bought.
    /// </remarks>
    [ConditionalFact]
    public Task An_inline_collection_of_parameters_matches_the_direct_query()
        => AssertSameStatement(
            (First: 1, Second: 3),
            static (blogs, ids) => blogs.Where(b => new[] { ids.First, ids.Second }.Contains(b.Id)));

    /// <summary>
    ///     A predicate built from captured values alone, with no column in it.
    /// </summary>
    /// <remarks>
    ///     EF evaluates the whole predicate on its own client and sends one boolean parameter. The
    ///     server ran <c>WHERE @Value</c> for
    ///     <c>NorthwindMiscellaneousQueryTestBase.Contains_over_concatenated_parameter_and_constant</c>,
    ///     and EF's SQLite suite asserts no SQL for that test, so this is where it is compared.
    /// </remarks>
    [ConditionalFact]
    public Task A_predicate_of_captured_values_matches_the_direct_query()
        => AssertSameStatement(
            "alpha",
            static (blogs, title) =>
            {
                string[] data = ["alpha" + "!", "beta" + "!"];
                return blogs.Where(b => ((IEnumerable<string>)data).Contains(title + "!"));
            });

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
    /// <param name="value">What the query captures.</param>
    /// <param name="query">The query, written once and run both ways.</param>
    /// <param name="collectionMode">
    ///     The server's collection mode, or <see langword="null" /> for EF's default.
    /// </param>
    private async Task AssertSameStatement<TValue, TResult>(
        TValue value,
        Func<IQueryable<Blog>, TValue, IQueryable<TResult>> query,
        ParameterTranslationMode? collectionMode = null)
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore(collectionMode: collectionMode);
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

    /// <summary>
    ///     A terminal operator above a projection this client reassembles.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The operator cannot ship — it consumes the rows the client puts back together — and
    ///         without the limit going with the shipped query the server sent every matching row and
    ///         the client kept one. EF's own client writes <c>LIMIT 1</c>, and its
    ///         <c>EnumTranslationsSqliteTest.HasFlag</c> is where the comparison with EF's SQL found it.
    ///     </para>
    ///     <para>
    ///         Only a projection the client reassembles is affected: a scalar or an entity projection
    ///         keeps the operator on the server, and a <c>Take</c> the caller wrote ships as it is.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public Task A_First_over_a_client_projection_matches_the_direct_query()
        => AssertSameStatementFor(
            static async blogs => _ = await blogs.Where(b => b.Id > 0).Select(b => new { b.Id, b.Title }).FirstAsync());

    /// <inheritdoc cref="A_First_over_a_client_projection_matches_the_direct_query" />
    [ConditionalFact]
    public Task A_FirstOrDefault_over_a_client_projection_matches_the_direct_query()
        => AssertSameStatementFor(
            static async blogs => _ = await blogs.Where(b => b.Id > 99).Select(b => new { b.Id, b.Title }).FirstOrDefaultAsync());

    /// <summary>
    ///     <c>Single</c>, whose limit is two: one row cannot show that a second exists, and the client
    ///     is what raises EF's "more than one element".
    /// </summary>
    /// <inheritdoc cref="A_First_over_a_client_projection_matches_the_direct_query" />
    [ConditionalFact]
    public Task A_Single_over_a_client_projection_matches_the_direct_query()
        => AssertSameStatementFor(
            static async blogs => _ = await blogs.Where(b => b.Id == 2).Select(b => new { b.Id, b.Title }).SingleAsync());

    /// <summary>
    ///     Paging above a projection this client reassembles.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A <c>Skip</c> or <c>Take</c> the caller writes after the projection stayed above the
    ///         client's rebuild, so the server sent every row and the client dropped the ones it
    ///         skipped. EF's own client writes <c>LIMIT</c> and <c>OFFSET</c>.
    ///         <c>Multi_level_includes_are_applied_with_skip</c> read every order of every customer
    ///         whose key starts with "A" that way, and passed, because it compares answers.
    ///     </para>
    ///     <para>
    ///         Found by running Tier B again with InfoCarrier removed and comparing each test
    ///         method's reads with the reads through the wire.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public Task A_Skip_over_a_client_projection_matches_the_direct_query()
        => AssertSameStatementFor(
            static async blogs => _ = await blogs.OrderBy(b => b.Id).Select(b => new { b.Id, b.Title }).Skip(1).ToListAsync());

    /// <inheritdoc cref="A_Skip_over_a_client_projection_matches_the_direct_query" />
    [ConditionalFact]
    public Task A_Take_over_a_client_projection_matches_the_direct_query()
        => AssertSameStatementFor(
            static async blogs => _ = await blogs.OrderBy(b => b.Id).Select(b => new { b.Id, b.Title }).Take(2).ToListAsync());

    /// <summary>
    ///     A terminal operator after paging above a projection this client reassembles, which is
    ///     the shape of <c>Multi_level_includes_are_applied_with_skip</c>.
    /// </summary>
    /// <remarks>
    ///     The <c>Skip</c> between the two used to hide the projection from the rule that sends
    ///     <c>First</c>'s row limit with the shipped query, so neither the offset nor the limit
    ///     reached the server.
    /// </remarks>
    [ConditionalFact]
    public Task A_First_after_Skip_over_a_client_projection_matches_the_direct_query()
        => AssertSameStatementFor(
            static async blogs => _ = await blogs.OrderBy(b => b.Id).Select(b => new { b.Id, b.Title }).Skip(1).FirstAsync());

    /// <summary>
    ///     Both paging operators between the terminal operator and the projection move, in the
    ///     order they were written.
    /// </summary>
    [ConditionalFact]
    public Task A_First_after_Skip_and_Take_over_a_client_projection_matches_the_direct_query()
        => AssertSameStatementFor(
            static async blogs => _ = await blogs.OrderBy(b => b.Id).Select(b => new { b.Id, b.Title }).Skip(1).Take(2).FirstAsync());

    /// <summary>
    ///     A split query keeps splitting when the row limit goes onto the shipped query.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The server puts the caller's <c>AsSplitQuery</c> back on the shipped query, and EF
    ///         refuses the hint on a tuple, which is what a rebuilt projection ships. It looked one
    ///         level down for an entity, and the limit put a <c>Take</c> in the way: the store ran
    ///         one joined statement where EF's own client runs one per collection. The rows were
    ///         bounded either way, so only the statements show it.
    ///     </para>
    ///     <para>
    ///         Found beside the paging fix, whose <c>Skip</c> added a second level:
    ///         <c>NorthwindSplitInclude.Multi_level_includes_are_applied_with_skip</c> went from
    ///         three unbounded statements to one bounded one.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_split_First_over_a_client_projection_matches_the_direct_query()
    {
        Run run = await RunBothWays(
            allowedTypes: null,
            static async context => _ = await context.Set<Blog>().AsSplitQuery().OrderBy(b => b.Id)
                .Select(b => new { b.Id, Posts = b.Posts.ToList() }).FirstAsync());

        Assert.Null(run.WireError);
        Assert.Equal(run.Directly, run.OverTheWire);
    }

    /// <inheritdoc cref="A_split_First_over_a_client_projection_matches_the_direct_query" />
    [ConditionalFact]
    public async Task A_split_First_after_Skip_over_a_client_projection_matches_the_direct_query()
    {
        Run run = await RunBothWays(
            allowedTypes: null,
            static async context => _ = await context.Set<Blog>().AsSplitQuery().OrderBy(b => b.Id)
                .Select(b => new { b.Id, Posts = b.Posts.ToList() }).Skip(1).FirstAsync());

        Assert.Null(run.WireError);
        Assert.Equal(run.Directly, run.OverTheWire);
    }

    /// <summary>
    ///     A projection that reads nothing from the row asks the store for no column, as plain
    ///     EF Core asks for none.
    /// </summary>
    /// <remarks>
    ///     EF's funcletizer lifts the whole anonymous object into one parameter and then needs the
    ///     store for the row count alone, so it writes <c>SELECT 1</c>. This client shipped the
    ///     table: the projection yields no fragment that reads the row, the rewrite gave up, and
    ///     the plain cut sent every column the entity has. Both answers are right, which is why no
    ///     test in the suite could see it.
    /// </remarks>
    [ConditionalFact]
    public Task A_projection_reading_no_column_matches_the_direct_query()
    {
        bool flag = true;

        return AssertSameStatementFor(
            async blogs => _ = await blogs.Select(b => new { F = flag }).ToListAsync());
    }

    /// <summary>
    ///     A <c>SelectMany</c> whose inner projection reads no column fails where plain EF Core
    ///     fails, with EF's own message, and runs no statement on either side.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The question this was written to ask, and the answer it gave instead.</b>
    ///         <c>TryHoistCollectionProjection</c> has a "no fragment" exit of its own, on the
    ///         same condition <see cref="A_projection_reading_no_column_matches_the_direct_query" />
    ///         is about one level up — and that one was a real defect, because the plain cut
    ///         shipped every column where EF writes <c>SELECT 1</c> (#155). So: does giving up
    ///         here strand the <c>SelectMany</c> on the client and ship <c>Blogs</c> whole?
    ///     </para>
    ///     <para>
    ///         <b>This query never reaches that exit, which is the finding.</b> Instrumented
    ///         2026-09-23: the hoist declines earlier, at <em>the body is <c>ServerOk</c></em>,
    ///         because EF's funcletizer lifts the whole <c>new { F = flag }</c> into one parameter.
    ///         The same run over the whole suite reached the "no fragment" exit 7 times and never
    ///         with 0 fragments. <c>ProjectionRewriter</c> carries the reading at the guard.
    ///     </para>
    ///     <para>
    ///         <b>What is left is still worth pinning</b>, and it is not what the name of the
    ///         defect above would suggest: both halves raise <c>InvalidOperationException</c> with
    ///         EF Core 10's own <c>'queryContext' could not be translated</c>, and the store sees
    ///         nothing from either side. There is no answer to lose and no table to ship. If the
    ///         hoist ever starts answering this, or starts reading a table to answer it, this test
    ///         says so.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_hoisted_projection_reading_no_column_fails_where_EF_fails()
    {
        bool flag = true;

        Run run = await RunBothWays(
            allowedTypes: null,
            async context => _ = await context.Set<Blog>()
                .SelectMany(b => b.Posts.Select(p => new { F = flag })).ToListAsync());

        Assert.Equal(
            Assert.IsType<InvalidOperationException>(run.DirectError).Message,
            Assert.IsType<InvalidOperationException>(run.WireError).Message);
        Assert.Empty(run.OverTheWire);
        Assert.Empty(run.Directly);
    }

    /// <summary>
    ///     A predicate that names <see cref="System.Type" /> reaches the server, and the server
    ///     runs the statement plain EF Core runs for it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>This is the evidence behind <c>security-review.md</c> §2's open recommendation.</b>
    ///         That recommendation says <c>typeof(Type)</c> earns its place on the allowlist only if
    ///         payloads genuinely carry <see cref="System.Type" /> values, and the suite could not
    ///         answer it: removing both admission sites on 2026-09-22 left the whole suite green
    ///         except the hardening test that asserts the pivot's own premise.
    ///     </para>
    ///     <para>
    ///         <c>GetType()</c> is where an ordinary query carries one: its return type is
    ///         <see cref="System.Type" /> and <c>typeof(Sparrow)</c> is a constant of it, so the
    ///         boundary needs the entry for a predicate EF translates to a discriminator test.
    ///     </para>
    ///     <para>
    ///         <b>The hierarchy is load-bearing, and the first version of this test did without
    ///         it.</b> Over a single mapped type the comparison is constant, EF folds it before
    ///         anything reaches the wire, and the test passed with the entry removed just as
    ///         happily as with it there. A probe that cannot fail measures nothing.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_predicate_naming_a_Type_matches_the_direct_query()
    {
        await AssertOneMatchingStatement(
            async c => _ = await c.Set<Creature>().Where(x => x.GetType() == typeof(Sparrow)).ToListAsync());
        await AssertOneMatchingStatement(
            async c => _ = await c.Set<Conveyance>().Where(x => x.GetType() == typeof(Sedan)).ToListAsync());
        await AssertOneMatchingStatement(
            async c => _ = await c.Set<Tool>().Where(x => x.GetType() == typeof(Hammer)).ToListAsync());
    }

    /// <summary>
    ///     <c>OfType&lt;T&gt;()</c> discriminates on the server under all three mappings, and needs
    ///     no <see cref="System.Type" /> value to do it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The type argument is an entity type and the declaring type is <c>Queryable</c>, so
    ///         nothing here depends on the allowlist entry the test above is about. Measured with
    ///         that entry removed: the statement is unchanged.
    ///     </para>
    ///     <para>
    ///         <b>Read from the statement, not from a green.</b> A differential test says "the same
    ///         as EF" and is silent when both sides read everything, so the three mappings were
    ///         each dumped and read: TPH filters on the discriminator, TPT joins the leaf table,
    ///         and TPC narrows to the one concrete table with no union.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task An_OfType_filter_matches_the_direct_query_under_every_mapping()
    {
        await AssertOneMatchingStatement(async c => _ = await c.Set<Creature>().OfType<Sparrow>().ToListAsync());
        await AssertOneMatchingStatement(async c => _ = await c.Set<Conveyance>().OfType<Sedan>().ToListAsync());
        await AssertOneMatchingStatement(async c => _ = await c.Set<Tool>().OfType<Hammer>().ToListAsync());
    }

    /// <summary>
    ///     Runs <paramref name="run" /> both ways and asserts the store saw <em>one</em> statement
    ///     each and the same one.
    /// </summary>
    /// <remarks>
    ///     The count is half the assertion. Matching text alone would still pass if this client
    ///     sent the query and then a second read, and an inheritance query is exactly where a
    ///     second read would hide.
    /// </remarks>
    private async Task AssertOneMatchingStatement(Func<DbContext, Task> run)
    {
        Run both = await RunBothWays(null, run);

        Assert.Null(both.WireError);
        Assert.Null(both.DirectError);
        Assert.Single(both.Directly);
        Assert.Single(both.OverTheWire);
        Assert.Equal(both.Directly[0], both.OverTheWire[0]);
    }

    /// <summary>
    ///     An ordering by a captured value the projection carries fails where plain EF Core fails,
    ///     and runs no statement.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The projection reads nothing from the row, so EF's funcletizer lifts the whole
    ///         anonymous object into one parameter, and the ordering reads a member of that
    ///         parameter, <c>(bool?)@p.F</c>, which EF cannot translate.
    ///         <c>NorthwindSelectQueryRelationalTestBase.Select_bool_closure_with_order_by_property_with_cast_to_nullable</c>
    ///         is EF's own test of that.
    ///     </para>
    ///     <para>
    ///         This client used to substitute that parameter as a constant of the anonymous type,
    ///         which the server does not have, so the split cut below the <c>Select</c>: the server
    ///         read the whole table and the client ordered the rows. The constant is now opened
    ///         into its construction, and the server's EF names <c>@p.Item1</c> where EF names
    ///         <c>@p.F</c>.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task An_ordering_by_a_captured_value_fails_where_EF_fails()
    {
        bool flag = false;
        Run run = await RunBothWays(
            allowedTypes: null,
            async context => _ = await context.Set<Blog>()
                .Select(b => new { F = flag })
                .OrderBy(e => (bool?)e.F)
                .ToListAsync());

        Assert.IsType<InvalidOperationException>(run.DirectError);
        Assert.IsType<InvalidOperationException>(run.WireError);
        Assert.Empty(run.OverTheWire);
    }

    /// <summary>
    ///     A <c>Distinct</c> over a projected collection that drops the element's key fails where
    ///     plain EF Core fails, with EF's own message, and runs no statement.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A relational provider attributes the rows of a collection to their owner after a
    ///         join, and <c>Distinct</c> over a projection without the key leaves it nothing to
    ///         attribute them by: EF raises <c>InsufficientInformationToIdentifyElementOfCollectionJoin</c>.
    ///         <c>GearsOfWarQueryRelationalTestBase.Correlated_collection_with_distinct_not_projecting_identifier_column_also_projecting_complex_expressions</c>
    ///         is EF's own test of that.
    ///     </para>
    ///     <para>
    ///         This client rebuilt the inner projection on the client, so the <c>Distinct</c> above
    ///         the rebuild ran here: the server joined the posts with no <c>DISTINCT</c> and the
    ///         client removed the duplicates.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_Distinct_over_a_projected_collection_without_its_key_fails_where_EF_fails()
    {
        Run run = await RunBothWays(
            allowedTypes: null,
            async context => _ = await context.Set<Blog>()
                .Select(b => new
                {
                    Key = b.Title,
                    Headings = b.Posts.Select(p => new { p.Heading, p.Heading!.Length }).Distinct().ToList(),
                })
                .ToListAsync());

        Assert.Equal(
            RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin,
            Assert.IsType<InvalidOperationException>(run.DirectError).Message);
        Assert.Equal(
            RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin,
            Assert.IsType<InvalidOperationException>(run.WireError).Message);
        Assert.Empty(run.OverTheWire);
    }

    /// <summary>
    ///     A projected collection whose elements read the enclosing row through a navigation fails
    ///     where plain EF Core fails, with EF's own message, and runs no statement.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF evaluates <c>p.Blog.Title</c> inside the correlated collection, which needs
    ///         <c>APPLY</c>, and SQLite has none: EF raises <c>ApplyNotSupported</c>.
    ///         <c>ComplexNavigationsCollectionsQuerySqliteTest.Projecting_collection_after_optional_reference_correlated_with_parent</c>
    ///         is EF's own test of that.
    ///     </para>
    ///     <para>
    ///         This client kept the enclosing read out of the inner tuple and carried it in a slot of
    ///         the outer one, so the server joined the tables without <c>APPLY</c> and the client
    ///         answered.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_projected_collection_reading_the_enclosing_row_fails_where_EF_fails()
    {
        Run run = await RunBothWays(
            allowedTypes: null,
            async context => _ = await context.Set<Post>()
                .Select(p => new
                {
                    p.Id,
                    Siblings = p.Blog!.Posts.Select(s => new { s.Id, p.Blog.Title }).ToList(),
                })
                .ToListAsync());

        Assert.Contains("APPLY", Assert.IsType<InvalidOperationException>(run.DirectError).Message, StringComparison.Ordinal);
        Assert.Equal(run.DirectError.Message, Assert.IsType<InvalidOperationException>(run.WireError).Message);
        Assert.Empty(run.OverTheWire);
    }

    /// <summary>
    ///     A projection over a client-typed projection runs plain EF Core's one statement, and its
    ///     subquery does not read a table of its own.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The second <c>Select</c> reads <c>x</c>, which only this client rebuilt, so it ran on
    ///         the client: the server ran <c>SELECT DISTINCT "Title" FROM "Blogs"</c> and, for the
    ///         subquery, the whole <c>Posts</c> table, where EF runs one <c>LEFT JOIN</c>. The two
    ///         projections are fused now, and the statement is EF's.
    ///     </para>
    ///     <para>
    ///         <b>One name differs and is mapped before the comparison.</b> The distinct subquery's
    ///         column is named after the member that carries it, <c>Item1</c> of the tuple where EF
    ///         has <c>Title</c> of the anonymous type. A column alias does not change the plan.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_projection_over_a_client_typed_projection_matches_the_direct_query()
    {
        Run run = await RunBothWays(
            allowedTypes: null,
            async context => _ = await context.Set<Blog>()
                .Select(b => new { b.Title })
                .Distinct()
                .Select(x => new
                {
                    x.Title,
                    Posts = context.Set<Post>().Where(p => p.Heading == x.Title).Select(p => new { p.Id }).ToList(),
                })
                .ToListAsync());

        Assert.Null(run.DirectError);
        Assert.Null(run.WireError);
        Assert.Equal(
            Assert.Single(run.Directly),
            Assert.Single(run.OverTheWire).Replace("\"Item1\"", "\"Title\"", StringComparison.Ordinal));
    }

    /// <summary>
    ///     A projection over the elements of a list stored in one column fails where plain EF Core
    ///     fails, and runs no statement, once the application registers the element type.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF raises <c>TranslationFailed</c> for the inner lambda, and
    ///         <c>CustomConvertersTestBase.Composition_over_collection_of_complex_mapped_as_scalar</c>
    ///         is its own test of that. The lambda names the element type, so it travels only when the
    ///         type is registered on both halves, and the server's EF then refuses it.
    ///     </para>
    ///     <para>
    ///         <b>Registration and not inference, the owner's decision of 2026-09-22.</b> Admitting the
    ///         element of every mapped collection property made framework types such as
    ///         <c>FileInfo</c> nameable by a payload (<c>security-review.md</c> §2b).
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_projection_over_a_converted_list_of_a_registered_type_fails_where_EF_fails()
    {
        Run registered = await RunBothWays([typeof(Tile)], ProjectTiles);

        Assert.IsType<InvalidOperationException>(registered.DirectError);
        Assert.IsType<InvalidOperationException>(registered.WireError);
        Assert.Empty(registered.OverTheWire);
    }

    /// <summary>
    ///     An <em>unregistered</em> element type reads the whole column here, and the client answers
    ///     a query EF refuses. This records it rather than fixing it.
    /// </summary>
    /// <remarks>
    ///     The same decision as <see cref="An_unregistered_group_key_reads_the_whole_table" />: a
    ///     type the application has not named keeps the operator on this client.
    ///     <see cref="A_projection_over_a_converted_list_of_a_registered_type_fails_where_EF_fails" />
    ///     is the other half.
    /// </remarks>
    [ConditionalFact]
    public async Task A_projection_over_a_converted_list_of_an_unregistered_type_reads_the_whole_column()
    {
        Run unregistered = await RunBothWays(allowedTypes: null, ProjectTiles);

        Assert.IsType<InvalidOperationException>(unregistered.DirectError);
        Assert.Null(unregistered.WireError);
        Assert.Contains("\"Tiles\"", Assert.Single(unregistered.OverTheWire), StringComparison.Ordinal);
    }

    private static async Task ProjectTiles(DbContext context)
        => _ = await context.Set<Panel>()
            .Select(p => new { p.Id, Tiles = p.Tiles.Select(t => new { H = t.Height, W = t.Width }).ToList() })
            .ToListAsync();

    /// <summary>
    ///     A grouping key of a type the application declares — the shape a real application writes,
    ///     and the one the specification suite's <c>NorthwindGroupBy.Odata_groupby_empty_key</c> is
    ///     an example of.
    /// </summary>
    private sealed class TitleKey(string? title)
    {
        public string? Title { get; } = title;

        public override bool Equals(object? obj)
            => obj is TitleKey other && other.Title == Title;

        public override int GetHashCode()
            => Title?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }

    /// <summary>
    ///     A grouping key the application has registered runs on the server, statement for
    ///     statement with plain EF Core.
    /// </summary>
    /// <remarks>
    ///     <b>This is what <see cref="InfoCarrierDbContextOptionsBuilder.AllowTypes" /> is for, and
    ///     the second half of what it is for was undocumented until this test.</b> The pages that
    ///     describe it say a registered type stops a query being REFUSED, and every example they
    ///     give is an <c>EF.Functions</c> family. It also decides WHERE THE WORK HAPPENS: the type
    ///     is what the boundary analyzer cannot ship, so without it the cut lands below the
    ///     <c>GroupBy</c> and the grouping runs here. With it, the statement is EF's own.
    /// </remarks>
    [ConditionalFact]
    public async Task A_registered_group_key_matches_the_direct_query()
    {
        Run wire = await RunBothWays(
            [typeof(TitleKey)],
            (blogs, _) => blogs.GroupBy(b => new TitleKey(b.Title)).Select(g => g.Count()));

        Assert.Equal(wire.Directly, wire.OverTheWire);
        Assert.Contains("GROUP BY", Assert.Single(wire.OverTheWire), StringComparison.Ordinal);
    }

    /// <summary>
    ///     An <em>unregistered</em> grouping key reads the whole table here, and this records the
    ///     cost rather than fixing it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>There is no error, and that is deliberate.</b>
    ///         <c>QuerySplitter.RejectClientEvaluation</c> throws EF's <c>TranslationFailed</c> for
    ///         client CODE in a row-deciding argument, and says in its own remarks why it does not
    ///         throw for a client TYPE: a <c>GroupBy</c> keyed on one is perfectly translatable and
    ///         lands here only because of this provider's type boundary, and refusing it cost 235
    ///         passing tests. So the answer is right, the payload is the whole table, and nothing
    ///         says so.
    ///     </para>
    ///     <para>
    ///         <b>The caller's fix is one line</b>, and
    ///         <see cref="A_registered_group_key_matches_the_direct_query" /> is the measurement:
    ///         name the type on both halves and the statement becomes EF's own. This test exists so
    ///         the untreated case cannot change silently, and so the pair reads as one decision.
    ///     </para>
    ///     <para>
    ///         <b>An anonymous key already ships and is not in this class</b> — measured
    ///         2026-09-21, <c>GroupBy(b =&gt; new { b.Title })</c> produces
    ///         <c>GROUP BY "b"."Title"</c> over the wire. Only a NAMED type the application has not
    ///         registered falls here.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task An_unregistered_group_key_reads_the_whole_table()
    {
        Run wire = await RunBothWays(
            allowedTypes: null,
            (blogs, _) => blogs.GroupBy(b => new TitleKey(b.Title)).Select(g => g.Count()));

        Assert.Contains("GROUP BY", Assert.Single(wire.Directly), StringComparison.Ordinal);

        string statement = Assert.Single(wire.OverTheWire);
        Assert.DoesNotContain("GROUP BY", statement, StringComparison.Ordinal);
        Assert.DoesNotContain("COUNT(", statement, StringComparison.Ordinal);
    }

    /// <summary>
    ///     An unregistered <em>join</em> key reads BOTH tables whole, and joins them here.
    /// </summary>
    /// <remarks>
    ///     <b>The same cause as the grouping key above, and the one #120 found by comparing the
    ///     server's SQL with EF's.</b> <c>JoinKeyRewriter</c> closed the ANONYMOUS case by giving
    ///     the key a <c>Tuple</c> the boundary already admits; a key of a type the application
    ///     declared cannot be rewritten that way, because a class with its own <c>Equals</c> is not
    ///     data. Registering it is what closes this one.
    /// </remarks>
    [ConditionalFact]
    public async Task An_unregistered_join_key_reads_both_tables()
    {
        Run wire = await RunBothWays(allowedTypes: null, JoinOnTitle);

        Assert.Contains("INNER JOIN", Assert.Single(wire.Directly), StringComparison.Ordinal);
        Assert.Equal(2, wire.OverTheWire.Length);
        Assert.All(wire.OverTheWire, s => Assert.DoesNotContain("JOIN", s, StringComparison.Ordinal));
    }

    /// <inheritdoc cref="A_registered_group_key_matches_the_direct_query" />
    [ConditionalFact]
    public async Task A_registered_join_key_matches_the_direct_query()
    {
        Run wire = await RunBothWays([typeof(TitleKey)], JoinOnTitle);

        Assert.Equal(wire.Directly, wire.OverTheWire);
        Assert.Contains("INNER JOIN", Assert.Single(wire.OverTheWire), StringComparison.Ordinal);
    }

    /// <inheritdoc cref="An_unregistered_join_key_reads_both_tables" />
    [ConditionalFact]
    public async Task An_unregistered_group_join_key_reads_both_tables()
    {
        Run wire = await RunBothWays(allowedTypes: null, GroupJoinOnTitle);

        Assert.Single(wire.Directly);
        Assert.Equal(2, wire.OverTheWire.Length);
    }

    /// <inheritdoc cref="A_registered_group_key_matches_the_direct_query" />
    [ConditionalFact]
    public async Task A_registered_group_join_key_matches_the_direct_query()
    {
        Run wire = await RunBothWays([typeof(TitleKey)], GroupJoinOnTitle);

        Assert.Equal(wire.Directly, wire.OverTheWire);
    }

    /// <summary>
    ///     Registering a key type can turn an answer into EF's own refusal, and that is the
    ///     behaviour to expect rather than a regression.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The most surprising half of "a registered type matches plain EF Core", and the
    ///         reason to state it as a promise.</b> <c>DistinctBy</c> keyed on a declared type is
    ///         one EF itself cannot translate: run directly against the server it raises
    ///         <c>TranslationFailed</c>. Unregistered, the key keeps the operator on this client,
    ///         the server sends every row and the caller gets an answer EF would have refused —
    ///         the <c>limitations.md</c> category of queries this provider answers that other EF
    ///         providers reject.
    ///     </para>
    ///     <para>
    ///         <b>Registering the type moves the operator to the server, where EF refuses it.</b>
    ///         So the registration that removes a whole-table read also removes the answer. Both
    ///         halves are "match plain EF Core", and a caller who registers a type needs to know
    ///         that is what they asked for.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_registered_key_makes_this_client_refuse_what_EF_refuses()
    {
        Run unregistered = await RunBothWays(allowedTypes: null, DistinctByTitle);

        Assert.NotNull(unregistered.DirectError);
        Assert.Null(unregistered.WireError);
        Assert.DoesNotContain("DISTINCT", Assert.Single(unregistered.OverTheWire), StringComparison.Ordinal);

        Run registered = await RunBothWays([typeof(TitleKey)], DistinctByTitle);

        Assert.NotNull(registered.WireError);
        Assert.NotNull(registered.DirectError);
        Assert.Equal(registered.DirectError.GetType(), registered.WireError.GetType());
        Assert.Empty(registered.OverTheWire);
    }

    private static IQueryable<int> JoinOnTitle(IQueryable<Blog> blogs, IQueryable<Post> posts)
        => blogs.Join(
            posts,
            b => new TitleKey(b.Title),
            p => new TitleKey(p.Heading),
            (b, p) => b.Id + p.Id);

    private static IQueryable<int> GroupJoinOnTitle(IQueryable<Blog> blogs, IQueryable<Post> posts)
        => blogs.GroupJoin(
            posts,
            b => new TitleKey(b.Title),
            p => new TitleKey(p.Heading),
            (b, ps) => b.Id);

    private static IQueryable<int> DistinctByTitle(IQueryable<Blog> blogs, IQueryable<Post> posts)
        => blogs.DistinctBy(b => new TitleKey(b.Title)).Select(b => b.Id);

    /// <summary>What the store saw for one query, run over the wire and again directly.</summary>
    private readonly record struct Run(
        string[] OverTheWire,
        Exception? WireError,
        string[] Directly,
        Exception? DirectError);

    /// <summary>
    ///     Runs <paramref name="query" /> over the wire and again directly against the server, and
    ///     reports every statement the store saw for each — and what each side threw, because a
    ///     registered key type can move an operator to the server and meet EF's own refusal there.
    /// </summary>
    private Task<Run> RunBothWays(
        Type[]? allowedTypes,
        Func<IQueryable<Blog>, IQueryable<Post>, IQueryable<int>> query)
        => RunBothWays(allowedTypes, async context => _ = await query(context.Set<Blog>(), context.Set<Post>()).ToListAsync());

    /// <summary>
    ///     Runs <paramref name="run" /> against the client context and again against the server
    ///     context, for a query that needs more of the model than blogs and posts.
    /// </summary>
    /// <inheritdoc cref="RunBothWays(Type[], Func{IQueryable{Blog}, IQueryable{Post}, IQueryable{int}})" />
    private async Task<Run> RunBothWays(Type[]? allowedTypes, Func<DbContext, Task> run)
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore(allowedTypes);
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(
                    new Blog { Id = 1, Title = "alpha" },
                    new Blog { Id = 2, Title = "beta" },
                    new Blog { Id = 3, Title = "beta" });
                context.AddRange(
                    new Post { Id = 1, Heading = "alpha", BlogId = 1 },
                    new Post { Id = 2, Heading = "beta", BlogId = 2 });
                await context.SaveChangesAsync();
            });

        Drain();

        Exception? wireError = null;
        await using (SqliteSmokeContext client = new(
            new DbContextOptionsBuilder<SqliteSmokeContext>()
                .UseInfoCarrier(
                    store,
                    o =>
                    {
                        if (allowedTypes is not null)
                        {
                            o.AllowTypes(allowedTypes);
                        }
                    })
                .Options))
        {
            try
            {
                await run(client);
            }
            catch (InvalidOperationException ex)
            {
                wireError = ex;
            }
        }

        string[] overTheWire = AllStatements(Drain());

        Exception? directError = null;
        using (DbContext server = store.CreateDbContext())
        {
            try
            {
                await run(server);
            }
            catch (InvalidOperationException ex)
            {
                directError = ex;
            }
        }

        return new Run(overTheWire, wireError, AllStatements(Drain()), directError);
    }

    /// <summary>
    ///     Runs <paramref name="run" /> over the wire and again directly against the server, and
    ///     asserts the store saw one statement, not two — for a query whose terminal operator cannot
    ///     be expressed as an <see cref="IQueryable{T}" />.
    /// </summary>
    private Task AssertSameStatementFor(Func<IQueryable<Blog>, Task> run)
        => AssertSameStatementFor(context => run(context.Set<Blog>()), collectionMode: null);

    /// <summary>
    ///     Runs <paramref name="run" /> against the client context and again against the server
    ///     context, and asserts the store saw one statement, not two — for a query that needs the
    ///     context itself, as a compiled query does.
    /// </summary>
    /// <param name="run">The query, written once and run both ways.</param>
    /// <param name="collectionMode">
    ///     The server's collection mode, or <see langword="null" /> for EF's default.
    /// </param>
    private async Task AssertSameStatementFor(Func<DbContext, Task> run, ParameterTranslationMode? collectionMode)
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore(collectionMode: collectionMode);
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
            await run(client);
        }

        string overTheWire = SingleStatement(Drain());

        using (DbContext server = store.CreateDbContext())
        {
            await run(server);
        }

        Assert.Equal(SingleStatement(Drain()), overTheWire);
    }

    /// <summary>
    ///     Runs <paramref name="run" /> against the client context and again against the server
    ///     context, for a query plain EF Core refuses, and returns what each threw and every statement
    ///     the store saw for the client.
    /// </summary>
    private async Task<(Exception OverTheWire, string[] WireStatements, Exception Directly)> RunFailingBothWays(
        Func<DbContext, Task> run,
        ParameterTranslationMode collectionMode)
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore(collectionMode: collectionMode);
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

        Exception overTheWire;
        await using (SqliteSmokeContext client = new(
            new DbContextOptionsBuilder<SqliteSmokeContext>().UseInfoCarrier(store).Options))
        {
            overTheWire = await Assert.ThrowsAnyAsync<Exception>(() => run(client));
        }

        string[] wireStatements = AllStatements(Drain());

        Exception directly;
        using (DbContext server = store.CreateDbContext())
        {
            directly = await Assert.ThrowsAnyAsync<Exception>(() => run(server));
        }

        return (overTheWire, wireStatements, directly);
    }

    private SqliteInfoCarrierBackendTestStore CreateStore(
        Type[]? allowedTypes = null,
        ParameterTranslationMode? collectionMode = null)
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SqliteSmokeContext),
                OnModelCreating = (_, _) => { },
                AllowedTypes = allowedTypes,
                OnAddOptions = b =>
                {
                    // The store's own option, on the server, which is where an application sets it.
                    // The client has no such option: it never translates to SQL.
                    if (collectionMode is { } mode)
                    {
                        new SqliteDbContextOptionsBuilder(b).UseParameterizedCollectionMode(mode);
                    }

                    // This class's own sink. It replaced the store's `ServerSqlLog` until
                    // 2026-09-24, because `LogTo` keeps one sink; the log is an interceptor now.
                    return b.LogTo(
                        line => { lock (_sink) { _sink.Add(line); } },
                        [RelationalEventId.CommandExecuted]);
                },
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
        => Normalize(Assert.Single(logged));

    /// <summary>Every statement in <paramref name="logged" />, each normalized as one.</summary>
    private static string[] AllStatements(string[] logged)
        => [.. logged.Select(Normalize)];

    /// <inheritdoc cref="SingleStatement" />
    private static string Normalize(string entry)
    {
        // The first two lines are the event header and the `Executed DbCommand (0ms) [...]`
        // preamble, whose elapsed time and parameter *names* both vary. The statement follows.
        string[] lines = entry.Split('\n');
        int start = Array.FindIndex(lines, l => l.TrimStart().StartsWith("SELECT", StringComparison.Ordinal));
        Assert.True(start >= 0, "no SELECT found in: " + entry);

        string sql = string.Join('\n', lines[start..]).Trim();

        // A column alias is a name too, and the same argument applies: the projection split sends a
        // tuple, so EF names a column `Item1` where the caller's projection called it `Id`. The
        // columns, their order and everything around them are what this compares, and
        // `eng/ef-sql-diff.py` ignores an alias for the same reason.
        //
        // The same holds for the alias a column is READ through, which the line above leaves in
        // place. EF names a `VALUES` table after its parameter, so the caller's `ids` gives
        // `"i"."Value"` and the box's `Value` gives `"v"."Value"` for the same statement.
        string unaliased = ColumnAlias().Replace(ParameterName().Replace(sql, "@p"), string.Empty);
        return AliasQualifier().Replace(unaliased, "\"_\".");
    }

    [GeneratedRegex(@"@[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex ParameterName();

    [GeneratedRegex(@"\s+AS ""[^""]*""")]
    private static partial Regex ColumnAlias();

    [GeneratedRegex(@"""[^""]*""\.")]
    private static partial Regex AliasQualifier();
}
