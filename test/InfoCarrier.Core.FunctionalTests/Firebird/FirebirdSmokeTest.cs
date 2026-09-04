// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Firebird;

/// <summary>
///     ADR-009 Tier C's vertical slice: a client context with no database queries through the
///     in-process transport against a server context on <b>embedded Firebird</b>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Tier B already proves that a relational server works, so this proves only what
///         Tier B cannot.</b> The first test is the round trip, because a tier that cannot answer
///         <c>SELECT</c> proves nothing else. The rest are the two capabilities that justify a
///         third tier at all: a scalar store function, a table-valued one, and the correlated
///         form of the second, which needs <c>APPLY</c>.
///     </para>
///     <para>
///         <b>The store is created and seeded by hand, as Tier B's smoke test is.</b> No fixture
///         is involved, so nothing here reads <c>SharedTestStoreProperties.ArbitrarySqlExecution</c>
///         and no raw string from a client is ever run: the DDL below is the <em>seed</em>,
///         executed on the server against the server's own context.
///     </para>
/// </remarks>
public class FirebirdSmokeTest
{
    private static FirebirdInfoCarrierBackendTestStore CreateStore()
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(FirebirdSmokeContext),

                // The base store hands this straight to TestModelSource, which does not accept
                // null; the context needs no customization beyond its own OnModelCreating.
                OnModelCreating = (_, _) => { },
            });

    private static FirebirdSmokeContext CreateClient(FirebirdInfoCarrierBackendTestStore store)
        => new(new DbContextOptionsBuilder<FirebirdSmokeContext>()
            .UseInfoCarrier(store)
            .Options);

    /// <summary>
    ///     Creates the database, its rows, and the two store routines the tests call.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Firebird has no <c>CREATE FUNCTION ... LANGUAGE SQL</c> shorthand and no
    ///         <c>CREATE TABLE FUNCTION</c>.</b> A scalar function is PSQL with a
    ///         <c>RETURN</c>, and a table-valued one is a <em>selectable</em> stored procedure:
    ///         it declares output parameters, loops, and <c>SUSPEND</c>s a row at a time. EF maps
    ///         both through <c>HasDbFunction</c> without knowing the difference.
    ///     </para>
    ///     <para>
    ///         Each statement goes in its own call because Firebird's parser takes one at a time.
    ///     </para>
    /// </remarks>
    private static async Task<FirebirdInfoCarrierBackendTestStore> SeededStoreAsync()
    {
        FirebirdInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(
                    new FbBlog { Id = 1, Title = "alpha" },
                    new FbBlog { Id = 2, Title = "beta" },
                    new FbPost { Id = 10, BlogId = 1, Heading = "first" },
                    new FbPost { Id = 11, BlogId = 1, Heading = "second" },
                    new FbPost { Id = 12, BlogId = 2, Heading = "third" });
                await context.SaveChangesAsync();

                await context.Database.ExecuteSqlRawAsync(
                    """
                    create function "PostCountOf" (blogId int)
                    returns int
                    as
                    begin
                        return (select count("Id") from "Posts" where "BlogId" = :blogId);
                    end
                    """);

                await context.Database.ExecuteSqlRawAsync(
                    """
                    create procedure "PostsOf" (blogId int)
                    returns ("PostId" int, "Heading" varchar(450))
                    as
                    begin
                        for select "Id", "Heading" from "Posts" where "BlogId" = :blogId
                        into :"PostId", :"Heading" do
                        begin
                            suspend;
                        end
                    end
                    """);
            });

        return store;
    }

    /// <summary>
    ///     The round trip. A tier that cannot answer this proves nothing else.
    /// </summary>
    [ConditionalFact]
    public async Task A_query_crosses_the_wire_and_the_Firebird_server_answers_it()
    {
        await using FirebirdInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using FirebirdSmokeContext client = CreateClient(store);

        List<string?> titles = await client.Blogs
            .OrderBy(b => b.Id)
            .Select(b => b.Title)
            .ToListAsync();

        Assert.Equal(["alpha", "beta"], titles);
    }

    /// <summary>
    ///     A scalar store function, mapped by <c>HasDbFunction</c> and run by the store.
    /// </summary>
    /// <remarks>
    ///     The method throws when invoked, so a client that evaluated it instead of sending it
    ///     would fail by name rather than return a plausible number.
    /// </remarks>
    [ConditionalFact]
    public async Task A_scalar_store_function_is_translated_and_run_on_the_server()
    {
        await using FirebirdInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using FirebirdSmokeContext client = CreateClient(store);

        var counts = await client.Blogs
            .OrderBy(b => b.Id)
            .Select(b => new { b.Id, Count = FirebirdSmokeContext.PostCountOf(b.Id) })
            .ToListAsync();

        Assert.Equal([(1, 2), (2, 1)], counts.Select(c => (c.Id, c.Count)));
    }

    /// <summary>
    ///     A table-valued function used as the <em>only</em> query root. <b>This is what Tier B
    ///     cannot do at all</b>, and until R154 this provider refused it as well.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>This pin was written because the Firebird tier separated two failures that
    ///         looked identical on SQLite.</b> Nine <c>UdfDbFunctionTestBase</c> tests were
    ///         classified together as "the store has no table-valued function". Reading their
    ///         messages one by one says otherwise: seven of them reach the store and answer
    ///         <c>no such table: GetTopTwoSellingProducts</c>, because their query root is a
    ///         <c>DbSet</c> and the function appears further in. <b>Two never reach it at all</b>,
    ///         <c>QF_Stand_Alone</c> and <c>QF_Stand_Alone_Parameter</c>, and they fail with the
    ///         message asserted below. Only a store that HAS the function can tell those apart,
    ///         which is what this tier is for.
    ///     </para>
    ///     <para>
    ///         <b>What is missing is a query root, not a translation.</b> Every queryable
    ///         function EF maps is an instance method on the context
    ///         (<c>FromExpression(() =&gt; PostsOf(blogId))</c>), so its receiver is the live
    ///         client context. That receiver now crosses as a stub, which is why the correlated
    ///         form below works: there the root is a <c>DbSet</c> and the call is only a node
    ///         inside it. When the call IS the root, <c>ServerBoundaryAnalyzer</c> finds a tree of
    ///         wholly expressible nodes with no query root in it and refuses the whole query,
    ///         which is the correct answer to the question it is asking and the wrong answer here.
    ///     </para>
    ///     <para>
    ///         <b>The fix is one clause, and it needed nothing plumbed in.</b> The marker is
    ///         created only for a method the model maps with <c>HasDbFunction</c>, so "receiver is
    ///         that marker and the call returns an <c>IQueryable</c>" already means "a mapped
    ///         queryable function", which is a query root. A mapped <em>scalar</em> function
    ///         reaches the same marker and is still not a root, which is right: it is a value
    ///         inside a query.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_table_valued_function_as_the_only_query_root_is_answered_by_the_server()
    {
        await using FirebirdInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using FirebirdSmokeContext client = CreateClient(store);

        List<string?> headings = await client.PostsOf(1)
            .OrderBy(p => p.PostId)
            .Select(p => p.Heading)
            .ToListAsync();

        Assert.Equal(["first", "second"], headings);
    }

    /// <summary>
    ///     The same function called with a value from the outer query, which EF compiles to
    ///     <c>CROSS APPLY</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>This test is the reason <see cref="FirebirdLateralQuerySqlGenerator" /> exists,
    ///         and it fails without it.</b> The Firebird provider emits
    ///         <c>JOIN LATERAL "PostsOf"("b"."Id") AS "p" ON TRUE</c>, and the store answers
    ///         <c>Token unknown</c>: a bare function is not a legal source after <c>LATERAL</c>,
    ///         though the same call wrapped in a derived table is.
    ///     </para>
    ///     <para>
    ///         It is also the shape behind fourteen of the failures Tier B cannot fix, because
    ///         SQLite has no <c>APPLY</c> and refuses the query one step earlier.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task A_correlated_table_valued_function_is_answered_through_APPLY()
    {
        await using FirebirdInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using FirebirdSmokeContext client = CreateClient(store);

        var rows = await (from blog in client.Blogs
                          from post in client.PostsOf(blog.Id)
                          orderby blog.Id, post.PostId
                          select new { blog.Id, post.Heading }).ToListAsync();

        Assert.Equal(
            [(1, "first"), (1, "second"), (2, "third")],
            rows.Select(r => (r.Id, r.Heading)));
    }
}
