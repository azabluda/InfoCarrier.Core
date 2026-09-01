// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

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
    private static SqliteInfoCarrierBackendTestStore CreateStore(
        Func<IServiceCollection, IServiceCollection>? onAddServices = null)
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SqliteSmokeContext),

                // The base store hands this straight to TestModelSource, which does not accept
                // null; SmokeContext needs no customization beyond its own OnModelCreating.
                OnModelCreating = (_, _) => { },
                OnAddServices = onAddServices,
            });

    private static SqliteSmokeContext CreateClient(
        SqliteInfoCarrierBackendTestStore store,
        Action<InfoCarrierDbContextOptionsBuilder>? infoCarrierOptions = null)
        => new(new DbContextOptionsBuilder<SqliteSmokeContext>()
            .UseInfoCarrier(store, infoCarrierOptions)
            .Options);

    private static async Task<SqliteInfoCarrierBackendTestStore> SeededStoreAsync(
        Func<IServiceCollection, IServiceCollection>? onAddServices = null)
    {
        SqliteInfoCarrierBackendTestStore store = CreateStore(onAddServices);
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

        return store;
    }

    // R85. The two halves of the allowed-types seam, and the closed default that makes it a seam
    // rather than a hole.
    //
    // `EF.Functions.Glob` is SQLite's, declared on `SqliteDbFunctionsExtensions` in the provider
    // assembly. `InfoCarrier.Core` references no provider and cannot name it, so before this the
    // call was refused at the client boundary while the server -- an ordinary SQLite provider --
    // translated it to `GLOB` without difficulty. `EF.Functions.Like` hid the gap for a whole
    // milestone, because `Like` is declared on EF's CORE `DbFunctionsExtensions` and always worked.
    //
    // The same story is every store's: `DateDiffDay` and `FreeText` are SQL Server's, and a
    // third-party provider has its own. The answer is not a list in this package -- it cannot
    // enumerate providers it does not reference -- but a registration the application makes, on
    // both sides, exactly as ADR-012 requires of a value mapper.
    [ConditionalFact]
    public async Task A_provider_specific_EF_Functions_call_is_refused_when_nothing_is_registered()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using SqliteSmokeContext client = CreateClient(store);

        // EF's own `TranslationFailed`, raised by `QuerySplitter.RejectClientEvaluation`. The
        // closed default of ADR-008 constraint 2: a type the model does not imply is not named.
        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blogs.CountAsync(b => EF.Functions.Glob(b.Title!, "al*")));

        Assert.Contains("could not be translated", refused.Message, StringComparison.Ordinal);
    }

    [ConditionalFact]
    public async Task A_provider_specific_EF_Functions_call_crosses_once_both_sides_admit_its_host()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync(
            services => services.AddInfoCarrierAllowedTypes(typeof(SqliteDbFunctionsExtensions)));

        await using SqliteSmokeContext client = CreateClient(
            store, o => o.AllowTypes(typeof(SqliteDbFunctionsExtensions)));

        // Two of the three titles do not start with "al"; the answer proves the server ran GLOB
        // rather than the client running something equivalent, because the client cannot run it
        // at all -- `SqliteDbFunctionsExtensions.Glob` throws when invoked.
        Assert.Equal(1, await client.Blogs.CountAsync(b => EF.Functions.Glob(b.Title!, "al*")));
        Assert.Equal(2, await client.Blogs.CountAsync(b => EF.Functions.Glob(b.Title!, "*a*")));
    }

    [ConditionalFact]
    public async Task Registering_the_host_on_the_client_alone_still_fails_on_the_server()
    {
        // ADR-012's rule restated for types, and the reason the server half is a separate call:
        // a type admitted on one side only is worse than one admitted on neither. The client now
        // ships the query and the SERVER refuses to read it, which is the half that is a security
        // boundary. Without this test the two registrations look like duplication.
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using SqliteSmokeContext client = CreateClient(
            store, o => o.AllowTypes(typeof(SqliteDbFunctionsExtensions)));

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blogs.CountAsync(b => EF.Functions.Glob(b.Title!, "al*")));

        Assert.Contains("deserialization allowlist", refused.Message, StringComparison.Ordinal);
    }

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
    public async Task A_foreign_key_set_from_a_principals_temporary_key_survives_the_round_trip()
    {
        // The dependent names its principal by *key* and not by navigation, which is the shape
        // `GraphUpdatesTestBase`'s `ChangeMechanism.Fk` uses. What travels is the client's
        // placeholder, so the server has to redirect it at the row the store actually issued.
        //
        // Note `Entry(...).Property(...).CurrentValue` rather than `alpha.Id`: EF keeps a
        // temporary key on the *entry*, not on the instance, so `alpha.Id` is still `0` here. That
        // is EF's behaviour on every provider and it is why the spec base reads the key this way
        // too. C76 recorded this shape as a suspected Tier B gap on the strength of a test that
        // read `alpha.Id`, wrote `0` into a required foreign key and got the `FOREIGN KEY
        // constraint failed` that deserved; C79 established there is no gap, and this test is what
        // says so.
        //
        // **What it guards, stated because it is less than it looks.** Three mutations were tried
        // and none turns it red: disabling the qualified placeholder lookup, disabling the
        // reference redirect entirely, and refusing to classify a foreign key as a reference at
        // all. On a store that issues keys at *save* every placeholder maps to itself, so the
        // redirect is a no-op and EF's own server-side fixup does the propagation. This is
        // therefore a characterization test for the round trip, not a guard on the redirect — the
        // guard on that is `InMemorySmokeTest`'s, on Tier A, where the store issues at `Add`.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext);

        await using (SqliteSmokeContext client = CreateClient(store))
        {
            var alpha = new Blog { Title = "alpha" };
            client.Add(alpha);
            client.Add(
                new Post
                {
                    Heading = "to-alpha",
                    BlogId = client.Entry(alpha).Property(x => x.Id).CurrentValue,
                });

            Assert.Equal(2, await client.SaveChangesAsync());
        }

        using DbContext server = store.CreateDbContext();
        Blog blog = await server.Set<Blog>().Include(x => x.Posts).SingleAsync();

        Assert.NotEqual(0, blog.Id);
        Assert.Equal(["to-alpha"], blog.Posts.Select(x => x.Heading));
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

    [ConditionalFact]
    public async Task A_deleted_row_releases_its_alternate_key_before_a_new_row_takes_it()
    {
        // The R44 originals audit's scenario pin: a `Deleted` and an `Added` row colliding on a
        // *unique constraint* — a table's primary key and its alternate keys — in one call, over
        // the wire, on a store that enforces it. R40 and R42 each found the wire dropping an
        // original that EF's command ordering needed, and this is the third place EF reads one.
        //
        // **What it does not do is prove the edge that reads it.** `AddUniqueValueEdges` builds
        // that edge from the deleted row's value `fromOriginalValues: true`, but `AddSameTableEdges`
        // already orders every `Deleted` command on a table before every `Added` one, with no value
        // read at all — so this passes either way. The audit's finding is that the path is closed
        // twice over: redundantly ordered here, and reading a value the wire cannot lose anyway,
        // because `Coded.Code` is a key property and EF fixes every key property's
        // `AfterSaveBehavior` at `Throw` (`Property.CheckAfterSaveBehavior` refuses any other
        // value), so a saved key's original always equals its current.
        //
        // Kept as a scenario pin rather than deleted with the audit: nothing else on Tier B sends
        // a delete and a colliding insert in one request.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Coded { Id = 1, Code = "X", Label = "before" });
                await context.SaveChangesAsync();
            });

        await using (SqliteSmokeContext client = CreateClient(store))
        {
            Coded existing = await client.Coded.SingleAsync();
            client.Remove(existing);

            // Same `Code`, in the same call. Without the DELETE first the store answers
            // `SQLite Error 19: 'UNIQUE constraint failed: Coded.Code'`.
            client.Add(new Coded { Id = 2, Code = "X", Label = "after" });

            Assert.Equal(2, await client.SaveChangesAsync());
        }

        using DbContext server = store.CreateDbContext();
        Coded saved = await server.Set<Coded>().SingleAsync();
        Assert.Equal((2, "X", "after"), (saved.Id, saved.Code, saved.Label));
    }

    [ConditionalFact]
    public async Task A_rolled_back_transaction_leaves_the_relational_store_untouched()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: _ => Task.CompletedTask);

        await using SqliteSmokeContext client = CreateClient(store);

        await using (await client.Database.BeginTransactionAsync())
        {
            client.Blogs.Add(new Blog { Id = 1, Title = "provisional" });
            await client.SaveChangesAsync();

            // Visible inside, because the query carries the same token and so runs on the
            // server context the transaction pinned.
            Assert.Equal(1, await client.Blogs.CountAsync());
        }

        await using SqliteSmokeContext after = CreateClient(store);
        Assert.Equal(0, await after.Blogs.CountAsync());
    }

    [ConditionalFact]
    public async Task A_committed_transaction_keeps_its_work()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: _ => Task.CompletedTask);

        await using SqliteSmokeContext client = CreateClient(store);

        await using (var transaction = await client.Database.BeginTransactionAsync())
        {
            client.Blogs.Add(new Blog { Id = 1, Title = "kept" });
            await client.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using SqliteSmokeContext after = CreateClient(store);
        Assert.Equal(["kept"], await after.Blogs.Select(b => b.Title).ToListAsync());
    }

    [ConditionalFact]
    public async Task A_savepoint_rolls_back_part_of_a_transaction()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: _ => Task.CompletedTask);

        await using SqliteSmokeContext client = CreateClient(store);

        await using (var transaction = await client.Database.BeginTransactionAsync())
        {
            // SQLite has savepoints, so this is the tier where the answer is not "no".
            Assert.True(transaction.SupportsSavepoints);

            client.Blogs.Add(new Blog { Id = 1, Title = "kept" });
            await client.SaveChangesAsync();

            await transaction.CreateSavepointAsync("sp");

            client.Blogs.Add(new Blog { Id = 2, Title = "undone" });
            await client.SaveChangesAsync();

            await transaction.RollbackToSavepointAsync("sp");
            await transaction.CommitAsync();
        }

        await using SqliteSmokeContext after = CreateClient(store);
        Assert.Equal(["kept"], await after.Blogs.Select(b => b.Title).ToListAsync());
    }

    [ConditionalFact]
    public async Task A_left_join_keeps_an_owned_value_that_has_no_public_member()
    {
        // R64. `LeftJoin` was missing from `ProjectionShape`'s operator list, so a left join's
        // result selector was never entered and every owned type it projected came back with no
        // entity type. That has two consequences and this test pins the loud one: the tracking
        // downgrade `ServerQueryExecutor.TrackingBehaviorFor` makes for an ownerless owned type
        // never fires, and the server refuses the query outright -- "a tracking query is
        // attempting to project an owned entity without a corresponding owner". Measured red
        // before the fix and green after.
        //
        // The quiet consequence is the worse one and this model does not reproduce it: with four
        // owned types sharing one CLR type, as `OwnedQueryTestBase` has, the mapper falls back to
        // the public CLR members and the value comes back with `City` set and `Line` -- which
        // lives in a private field behind an indexer -- silently missing.
        // `OwnedQueryRelationalTestBase.Left_join_on_entity_with_owned_navigations` is what covers
        // that half, and this repository does not run it yet (R62).
        //
        // `Line` is asserted beside `City` because the defect took one and left the other.
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Blog { Id = 1, Title = "alpha" });
                context.Add(
                    new Located { Id = 1, Address = new LocatedAddress { City = "Zurich", ["Line"] = "Bahnhofstrasse 1" } });
                await context.SaveChangesAsync();
            });

        await using SqliteSmokeContext client = CreateClient(store);

        var rows = await client.Blogs
            .LeftJoin(client.Located, b => b.Id, l => l.Id, (b, l) => new { b.Title, Address = l!.Address })
            .ToListAsync();

        var row = Assert.Single(rows);
        LocatedAddress address = Assert.IsType<LocatedAddress>(row.Address);
        Assert.Equal("Zurich", address.City);
        Assert.Equal("Bahnhofstrasse 1", address["Line"]);
    }
}
