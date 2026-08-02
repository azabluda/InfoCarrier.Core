// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     The relational tier's vertical slice (ADR-009 Tier B): a client context with no database
///     queries through the in-process transport against a server context on **SQLite**.
/// </summary>
/// <remarks>
///     Tier A proves the pipeline; this proves it against a provider that actually translates.
///     EF's InMemory provider client-evaluates nearly everything, so it cannot distinguish a
///     query this provider gets wrong from one InMemory simply cannot do — and it cannot test
///     transactions at all, since it raises <c>TransactionIgnoredWarning</c> as an error.
/// </remarks>
public class SqliteSmokeTest
{
    private static SqliteInfoCarrierBackendTestStore CreateStore()
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SqliteSmokeContext),

                // The base store hands this straight to TestModelSource, which does not accept
                // null; SmokeContext needs no customization beyond its own OnModelCreating.
                OnModelCreating = (_, _) => { },
            });

    private static SqliteSmokeContext CreateClient(SqliteInfoCarrierBackendTestStore store)
        => new(new DbContextOptionsBuilder<SqliteSmokeContext>().UseInfoCarrier(store).Options);

    [ConditionalFact]
    public async Task Client_query_round_trips_through_a_relational_server()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(
                    new Blog { Id = 1, Title = "alpha" },
                    new Blog { Id = 2, Title = "beta" });
                await context.SaveChangesAsync();
            });

        await using SqliteSmokeContext client = CreateClient(store);
        List<Blog> blogs = await client.Blogs.OrderBy(b => b.Id).ToListAsync();

        Assert.Equal(["alpha", "beta"], blogs.Select(b => b.Title));
    }

    [ConditionalFact]
    public async Task A_projection_is_split_and_answered_against_a_relational_server()
    {
        // The projection split's own claim, checked where it matters: the server translates a
        // real query and returns only the projected column, and the client rebuilds its own type.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(
                    new Blog { Id = 1, Title = "alpha" },
                    new Blog { Id = 2, Title = "beta" });
                await context.SaveChangesAsync();
            });

        await using SqliteSmokeContext client = CreateClient(store);
        var rows = await client.Blogs
            .Where(b => b.Id > 1)
            .Select(b => new { b.Title, Length = b.Title!.Length })
            .ToListAsync();

        Assert.Equal("beta", Assert.Single(rows).Title);
        Assert.Equal(4, rows[0].Length);
    }

    [ConditionalFact]
    public async Task The_store_keeps_one_connection_open_for_its_lifetime()
    {
        // An in-memory SQLite database is destroyed when its last connection closes. If the
        // store let EF open and close per context, the schema and seed would vanish between
        // operations — so this asserts the ADR-009 requirement directly rather than trusting it.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Blog { Id = 1, Title = "alpha" });
                await context.SaveChangesAsync();
            });

        // A second, independent server context must still see the seeded row.
        using DbContext second = store.CreateDbContext();
        Assert.Equal(1, await second.Set<Blog>().CountAsync());
    }

    [ConditionalFact]
    public async Task Insert_update_and_delete_round_trip_through_the_server()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext);

        // Insert. Id is store-generated, so the client holds a temporary key until the server
        // reports the real one back by correlation id (research-findings §9).
        await using (SqliteSmokeContext client = CreateClient(store))
        {
            var blog = new Blog { Title = "alpha" };
            client.Add(blog);
            Assert.Equal(1, await client.SaveChangesAsync());
            Assert.NotEqual(0, blog.Id);
        }

        using (DbContext server = store.CreateDbContext())
        {
            Assert.Equal("alpha", (await server.Set<Blog>().SingleAsync()).Title);
        }

        // Update.
        await using (SqliteSmokeContext client = CreateClient(store))
        {
            Blog blog = await client.Blogs.SingleAsync();
            blog.Title = "beta";
            Assert.Equal(1, await client.SaveChangesAsync());
        }

        using (DbContext server = store.CreateDbContext())
        {
            Assert.Equal("beta", (await server.Set<Blog>().SingleAsync()).Title);
        }

        // Delete.
        await using (SqliteSmokeContext client = CreateClient(store))
        {
            client.Remove(await client.Blogs.SingleAsync());
            Assert.Equal(1, await client.SaveChangesAsync());
        }

        using (DbContext server = store.CreateDbContext())
        {
            Assert.Empty(await server.Set<Blog>().ToListAsync());
        }
    }

    [ConditionalFact]
    public async Task A_store_generated_key_comes_back_on_the_client_entity()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext);

        await using SqliteSmokeContext client = CreateClient(store);
        var first = new Blog { Title = "alpha" };
        var second = new Blog { Title = "beta" };
        client.AddRange(first, second);

        Assert.Equal(2, await client.SaveChangesAsync());

        // Distinct, non-temporary, and matched to the right entity — the correlation id is what
        // keeps the second row's key off the first entity.
        Assert.NotEqual(first.Id, second.Id);
        Assert.All([first.Id, second.Id], id => Assert.NotEqual(0, id));
        Assert.Equal(
            "alpha",
            (await client.Blogs.SingleAsync(b => b.Id == first.Id)).Title);
    }

    [ConditionalFact]
    public async Task A_new_dependent_of_a_new_principal_gets_the_generated_foreign_key()
    {
        // The case the correlation id exists for. Blog.Id is store-generated, so on the client
        // Post.BlogId is a *temporary* value; sending it would insert a row pointing at an id
        // the store never issued. The relationship travels instead, and EF's fixup on the server
        // supplies the real foreign key once the blog is inserted (research-findings §9).
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext);

        await using (SqliteSmokeContext client = CreateClient(store))
        {
            var blog = new Blog { Title = "alpha" };
            blog.Posts.Add(new Post { Heading = "first" });
            blog.Posts.Add(new Post { Heading = "second" });
            client.Add(blog);

            Assert.Equal(3, await client.SaveChangesAsync());
            Assert.NotEqual(0, blog.Id);
            Assert.All(blog.Posts, p => Assert.Equal(blog.Id, p.BlogId));
        }

        using DbContext server = store.CreateDbContext();
        Blog saved = await server.Set<Blog>().Include(b => b.Posts).SingleAsync();
        Assert.Equal(["first", "second"], saved.Posts.OrderBy(p => p.Heading).Select(p => p.Heading));
        Assert.All(saved.Posts, p => Assert.Equal(saved.Id, p.BlogId));
    }

    [ConditionalFact]
    public async Task A_dependent_of_an_existing_principal_travels_by_foreign_key()
    {
        // No link needed here: the blog already exists, so the foreign key is a real value and
        // goes across as an ordinary property.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext);

        int blogId;
        await using (SqliteSmokeContext client = CreateClient(store))
        {
            var blog = new Blog { Title = "alpha" };
            client.Add(blog);
            await client.SaveChangesAsync();
            blogId = blog.Id;
        }

        await using (SqliteSmokeContext client = CreateClient(store))
        {
            client.Add(new Post { Heading = "later", BlogId = blogId });
            Assert.Equal(1, await client.SaveChangesAsync());
        }

        using DbContext server = store.CreateDbContext();
        Assert.Equal(blogId, (await server.Set<Post>().SingleAsync()).BlogId);
    }

    [ConditionalFact]
    public async Task A_many_to_many_link_between_two_new_entities_is_persisted()
    {
        // The hardest SaveChanges shape, and the one ADR-004 calls v1's worst failure mode. The
        // join entity is a shared-type entity with two foreign keys and no navigations, and both
        // of those keys are temporary because both principals are new. Nothing but the shared
        // temporary value connects the three rows.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext);

        await using (SqliteSmokeContext client = CreateClient(store))
        {
            var post = new Post { Heading = "first", Blog = new Blog { Title = "alpha" } };
            post.Tags.Add(new Tag { Label = "ef" });
            post.Tags.Add(new Tag { Label = "linq" });
            client.Add(post);

            await client.SaveChangesAsync();
        }

        using DbContext server = store.CreateDbContext();
        Post saved = await server.Set<Post>().Include(p => p.Tags).SingleAsync();
        Assert.Equal(["ef", "linq"], saved.Tags.OrderBy(t => t.Label).Select(t => t.Label));
    }

    [ConditionalFact]
    public async Task A_many_to_many_link_between_existing_entities_is_persisted()
    {
        // Here the join entity is the *only* changed entry: both principals already exist, so
        // neither appears in the request and the link has to stand on its own.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext);

        await using (SqliteSmokeContext seed = CreateClient(store))
        {
            seed.Add(new Post { Heading = "first", Blog = new Blog { Title = "alpha" } });
            seed.Add(new Tag { Label = "ef" });
            await seed.SaveChangesAsync();
        }

        await using (SqliteSmokeContext client = CreateClient(store))
        {
            Post post = await client.Posts.Include(p => p.Tags).SingleAsync();
            post.Tags.Add(await client.Tags.SingleAsync());
            Assert.Equal(1, await client.SaveChangesAsync());
        }

        using DbContext server = store.CreateDbContext();
        Post saved = await server.Set<Post>().Include(p => p.Tags).SingleAsync();
        Assert.Equal("ef", Assert.Single(saved.Tags).Label);
    }
}
