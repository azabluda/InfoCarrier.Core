// Licensed under the MIT license. See license.txt file in the project root for license information.

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
    ///     Runs <paramref name="query" /> over the wire and again directly against the server, and
    ///     asserts the store saw one statement, not two.
    /// </summary>
    private async Task AssertSameStatement<TValue>(
        TValue value,
        Func<IQueryable<Blog>, TValue, IQueryable<Blog>> query)
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
