// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     The first end-to-end smoke test (E3): a client context using the InfoCarrier provider
///     queries through the in-process transport against a server context on the InMemory
///     provider. Proves the vertical slice: capture → serialize → rebind → execute →
///     materialize.
/// </summary>
public class InMemorySmokeTest
{
    private static IServiceProvider BuildServerProvider(string databaseName)
        => new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
            .AddScoped<IExpressionSerializer, ExpressionSerializer>()
            .AddScoped<InfoCarrier.Core.Expressions.TypeNodeMapper>()
            .AddScoped<InfoCarrier.Core.Expressions.TypeNodeResolver>()
            .AddScoped<InfoCarrier.Core.Expressions.IDynamicValueMapper, InfoCarrier.Core.Expressions.DynamicValueMapper>()
            .AddScoped<InfoCarrier.Core.Expressions.ExpressionToNodeTranslator>()
            .AddDbContext<SmokeContext>(b => b.UseInMemoryDatabase(databaseName))
            .AddScoped<DbContext>(sp => sp.GetRequiredService<SmokeContext>())
            .BuildServiceProvider(validateScopes: true);

    [Fact]
    public async Task Client_query_round_trips_through_server()
    {
        string databaseName = Guid.NewGuid().ToString();

        // Seed the server store directly.
        IServiceProvider serverProvider = BuildServerProvider(databaseName);
        using (IServiceScope scope = serverProvider.CreateScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<SmokeContext>();
            seed.Blogs.AddRange(
                new Blog { Id = 1, Title = "alpha" },
                new Blog { Id = 2, Title = "beta" });
            seed.SaveChanges();
        }

        // The in-process client: ships operations to the in-process server over the transport.
        var server = new InProcessInfoCarrierServer(serverProvider);

        // The product's own envelope server, not a hand-rolled dispatcher. This test used to
        // carry one that handled `Query` and threw for the other eight operations, which is what
        // hid the fact that the product had no server half of the envelope protocol at all (C45).
        var envelopeServer = new InfoCarrierEnvelopeServer(server, new SystemTextJsonInfoCarrierSerializer());
        var transport = new InProcessInfoCarrierTransport(
            envelopeServer.DispatchAsync,
            new SystemTextJsonInfoCarrierSerializer(),
            envelopeServer.DispatchQueryAsync);
        var client = new TransportInfoCarrierClient(transport, new SystemTextJsonInfoCarrierSerializer());

        // Let EF build its own internal service provider via the InfoCarrier options
        // extension's ApplyServices (which calls AddEntityFrameworkInfoCarrier).
        var clientOptions = new DbContextOptionsBuilder<SmokeContext>()
            .UseInfoCarrier(client)
            .Options;

        await using var context = new SmokeContext(clientOptions);
        List<Blog> blogs = await context.Blogs.OrderBy(b => b.Id).ToListAsync();

        Assert.Equal(2, blogs.Count);
        Assert.Equal("alpha", blogs[0].Title);
        Assert.Equal("beta", blogs[1].Title);
    }


    private static InMemoryInfoCarrierBackendTestStore CreateStore()
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SmokeContext),
                OnModelCreating = (_, _) => { },
            });

    private static SmokeContext CreateClient(InMemoryInfoCarrierBackendTestStore store)
        => new(new DbContextOptionsBuilder<SmokeContext>().UseInfoCarrier(store).Options);

    [ConditionalFact]
    public async Task A_foreign_key_placeholder_resolves_against_its_own_principal_type()
    {
        // C76, pinned on **Tier A** and not Tier B, which is the part worth knowing. A client
        // placeholder is not unique across entity types: EF's temporary generator counts down from
        // `int.MinValue` *per key property*, so `Blog.Id` and `Post.Id` issue the same numbers in
        // one request. The server's map from placeholder to real key was keyed by the value alone,
        // so a post's own registration replaced a blog's and the next post's foreign key resolved
        // to the post -- a wrong foreign key written to the store.
        //
        // **Only a store that issues keys at `Add` can show it.** On Tier B every registration
        // maps a placeholder to *itself* (the store has nothing better to offer until save), so
        // the overwrite is harmless and EF's own propagation does the work. The InMemory provider
        // issues the real key at `Add`, which is what makes the wrong entry observable -- and it
        // is why `GraphUpdatesTestBase`, a Tier A base, is where this surfaced.
        //
        // The crossing is the other half. Tracking is principal-first, so the first post resolves
        // before any post has registered; it is the second, pointing back at the *first* blog,
        // whose placeholder the first post has by then taken over.
        //
        // `Entry(...).Property(...).CurrentValue` rather than `alpha.Id`: EF keeps a temporary key
        // on the entry, not on the instance, so `alpha.Id` is still `0` here. That is EF's
        // behaviour on every provider, and it is how the spec base reads the key too.
        await using InMemoryInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                // The two key sequences have to be *apart* for the collision to be visible. Both
                // start at 1 in a fresh InMemory store, so the wrong map entry happens to hold the
                // right number and the defect hides. Three seeded posts put `Post.Id` ahead of
                // `Blog.Id`, which is the situation `GraphUpdates` is in after its own seed.
                var seeded = new Blog { Title = "seed" };
                seeded.Posts.Add(new Post { Heading = "s1" });
                seeded.Posts.Add(new Post { Heading = "s2" });
                seeded.Posts.Add(new Post { Heading = "s3" });
                context.Add(seeded);
                await context.SaveChangesAsync();
            });

        await using (SmokeContext client = CreateClient(store))
        {
            var alpha = new Blog { Title = "alpha" };
            var beta = new Blog { Title = "beta" };
            client.AddRange(alpha, beta);

            client.AddRange(
                new Post
                {
                    Heading = "to-beta",
                    BlogId = client.Entry(beta).Property(x => x.Id).CurrentValue,
                },
                new Post
                {
                    Heading = "to-alpha",
                    BlogId = client.Entry(alpha).Property(x => x.Id).CurrentValue,
                });

            Assert.Equal(4, await client.SaveChangesAsync());
        }

        using DbContext server = store.CreateDbContext();
        List<Blog> blogs = await server.Set<Blog>().Include(x => x.Posts).ToListAsync();

        Assert.Equal(["to-alpha"], blogs.Single(x => x.Title == "alpha").Posts.Select(p => p.Heading));
        Assert.Equal(["to-beta"], blogs.Single(x => x.Title == "beta").Posts.Select(p => p.Heading));
    }

    /// <summary>
    ///     An interface <b>no entity type implements</b>, which is the whole point of it:
    ///     <see cref="TypeAllowlist" /> admits every interface a mapped type implements, so this
    ///     is the one shape a <c>Cast</c>/<c>OfType</c> type argument can take and still be
    ///     unshippable.
    /// </summary>
    public interface IUnmappedMarker
    {
        int Id { get; set; }
    }

    /// <summary>
    ///     J18: a <c>Cast&lt;T&gt;</c> or <c>OfType&lt;T&gt;</c> the wire cannot carry is refused
    ///     the way EF refuses it, rather than answered by <c>Enumerable</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>What this looked like before the guard</b>, printed by the probe that found it
    ///         against this very seed:
    ///     </para>
    ///     <code>
    ///     OfType  => 0 row(s)
    ///     Cast    => InvalidCastException: Unable to cast object of type 'Blog' to 'IUnmappedMarker'
    ///     control => 2 row(s)
    ///     </code>
    ///     <para>
    ///         The <c>OfType</c> line is the one worth a test. Zero rows and no error is what
    ///         <c>Enumerable.OfType</c> does, and it is not what EF does — EF refuses the query.
    ///         <b>It is a missing diagnostic and not a wrong answer</b>: the only type that can
    ///         reach here is one no entity implements, for which LINQ-to-objects also answers
    ///         empty. That distinction is recorded because J18 first assumed the stronger one.
    ///     </para>
    ///     <para>
    ///         <b>The control is what makes the test non-vacuous</b>, and it is not decoration:
    ///         <c>OfType&lt;Blog&gt;</c> over the same seed returns 2. So the store has data, the
    ///         query path works, and the type argument is the only thing that differs between the
    ///         refusal and the answer. Without it, a guard that refused <em>every</em>
    ///         <c>OfType</c> would pass this test.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task An_unshippable_type_argument_is_refused_rather_than_answered_by_Enumerable()
    {
        await using InMemoryInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.AddRange(new Blog { Title = "alpha" }, new Blog { Title = "beta" });
                await context.SaveChangesAsync();
            });

        await using SmokeContext client = CreateClient(store);

        // Was: 0 rows, no error.
        Assert.Contains(
            "could not be translated",
            Assert.Throws<InvalidOperationException>(
                () => client.Blogs.OfType<IUnmappedMarker>().ToList()).Message);

        // Was: InvalidCastException out of Enumerable.Cast.
        Assert.Contains(
            "could not be translated",
            Assert.Throws<InvalidOperationException>(
                () => client.Blogs.Cast<IUnmappedMarker>().ToList()).Message);

        // The non-vacuity control: a type argument the model does name still answers.
        Assert.Equal(2, client.Blogs.OfType<Blog>().ToList().Count);

        // And so does the redundant cast EF elides -- `object` is assignable from everything, so
        // the guard must not touch it.
        Assert.Equal(2, client.Blogs.Cast<object>().ToList().Count);
    }
}
