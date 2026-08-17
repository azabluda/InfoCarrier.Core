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
            new SystemTextJsonInfoCarrierSerializer());
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
}
