// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
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


    [ConditionalFact]
    public async Task The_server_stops_a_query_when_the_caller_cancels()
    {
        // THE SERVER SIDE IS WHAT THIS ASSERTS, and no test EF ships can. EF's own
        // `ToListAsync_can_be_canceled` and `ToListAsync_with_canceled_token` are both green here
        // and both only ask what the CALLER saw; in EF's model there is no second process for work
        // to continue in, so there is nothing else to ask. Here there is: the token reaches
        // `ServerQueryExecutor` from the transport, and it used to be accepted and dropped, so a
        // client that cancelled stopped waiting while the server read the store to the end.
        //
        // Capture a real request and replay it straight into the server with a cancelled token.
        // That removes the client from the picture entirely -- a client-side cancel would abort
        // the transport first and prove nothing about the far side.
        string databaseName = Guid.NewGuid().ToString();

        IServiceProvider serverProvider = BuildServerProvider(databaseName);
        using (IServiceScope scope = serverProvider.CreateScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<SmokeContext>();
            seed.Blogs.AddRange(new Blog { Id = 1, Title = "alpha" }, new Blog { Id = 2, Title = "beta" });
            seed.SaveChanges();
        }

        var server = new InProcessInfoCarrierServer(serverProvider);
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var envelopeServer = new InfoCarrierEnvelopeServer(server, serializer);

        InfoCarrierEnvelope? capturedQuery = null;
        var transport = new InProcessInfoCarrierTransport(
            async (envelope, token) =>
            {
                if (envelope.Operation == InfoCarrierOperation.Query)
                {
                    capturedQuery = envelope;
                }

                return await envelopeServer.DispatchAsync(envelope, token);
            },
            serializer);

        var clientOptions = new DbContextOptionsBuilder<SmokeContext>()
            .UseInfoCarrier(new TransportInfoCarrierClient(transport, serializer))
            .Options;

        await using (var context = new SmokeContext(clientOptions))
        {
            Assert.Equal(2, (await context.Blogs.ToListAsync()).Count);
        }

        QueryDataRequest request = serializer.Deserialize<QueryDataRequest>(capturedQuery!.Payload)!;

        // The same request the run above answered with two rows, now with a cancelled token.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => server.QueryDataAsync(request, new CancellationToken(true)));
    }

    /// <summary>
    ///     A type the server's allowlist does not admit, so a call to it cannot be shipped.
    /// </summary>
    public static class ClientOnly
    {
        public static string Describe(string? title) => $"<{title}>";

        public static bool TitleIsLong(string? title) => title is { Length: > 4 };
    }

    [ConditionalFact]
    public async Task A_projection_the_server_cannot_run_is_logged_as_a_split()
    {
        // ESTABLISHES THAT THE EVENT FIRES, which is the only reason this test exists. A log
        // nobody raises and a log nobody needs look identical from outside, and this repository
        // has read that ambiguity wrongly before.
        //
        // A projection is the shape that splits. `ClientOnly` is not on the type allowlist, so
        // the `Select` cannot ship; the server runs the filtered query and this client builds the
        // projection over the rows that came back. Note the `Where` still ships, which is the
        // property the guide states and the reason this split is safe rather than merely legal.
        await using InMemoryInfoCarrierBackendTestStore store = CreateStore();

        await using (SmokeContext seed = CreateClient(store))
        {
            seed.Blogs.AddRange(
                new Blog { Id = 1, Title = "alpha" },
                new Blog { Id = 2, Title = "be" });
            await seed.SaveChangesAsync();
        }

        var log = new List<string>();

        await using (var context = new SmokeContext(
            new DbContextOptionsBuilder<SmokeContext>()
                .UseInfoCarrier(store)
                .LogTo(log.Add, [InfoCarrierEventId.QuerySplit])
                .Options))
        {
            var described = await context.Blogs
                .Where(b => b.Id == 1)
                .Select(b => new { b.Id, Label = ClientOnly.Describe(b.Title) })
                .ToListAsync();

            Assert.Equal("<alpha>", Assert.Single(described).Label);
        }

        Assert.Contains(log, line => line.Contains("Part of the query cannot be sent to the server"));
    }

    [ConditionalFact]
    public async Task A_query_the_server_runs_whole_logs_no_split()
    {
        // The other half, and the one that keeps the event honest: the common case must stay
        // silent. Without this, an event that fired on every query would still pass the test
        // above.
        await using InMemoryInfoCarrierBackendTestStore store = CreateStore();

        await using (SmokeContext seed = CreateClient(store))
        {
            seed.Blogs.Add(new Blog { Id = 1, Title = "alpha" });
            await seed.SaveChangesAsync();
        }

        var log = new List<string>();

        await using (var context = new SmokeContext(
            new DbContextOptionsBuilder<SmokeContext>()
                .UseInfoCarrier(store)
                .LogTo(log.Add, [InfoCarrierEventId.QuerySplit])
                .Options))
        {
            List<Blog> matched = await context.Blogs.Where(b => b.Title == "alpha").ToListAsync();

            Assert.Single(matched);
        }

        Assert.Empty(log);
    }

    [ConditionalFact]
    public async Task A_filter_the_server_cannot_run_throws_rather_than_fetching_everything()
    {
        // PINS `RejectClientEvaluation`, AND IT WAS WRITTEN AFTER GETTING THIS WRONG. Reading
        // projection-split.md §3.3 ("a `Where` over a client type ... falls back to §3.5") and
        // §3.5 (ship the maximal ServerOk subtree containing a query root) says the cut lands
        // below the `Where`, the server runs the query root alone, and the whole table crosses
        // the wire silently. `ServerBoundaryAnalyzer` agrees. Both are describing the *frontier*,
        // and neither mentions the guard that runs between them.
        //
        // `QuerySplitter.RejectClientEvaluation` allows client-side work only where it is a
        // projection reassembly. A `Where` is not one, so this throws EF's own
        // `TranslationFailedWithDetails` and this provider converges with every other EF
        // provider. There is no silent full fetch. THE DESIGN DOCUMENT PLUS THE ANALYZER WAS NOT
        // ENOUGH TO KNOW THAT, and this test is what closed the gap between reading and knowing.
        await using InMemoryInfoCarrierBackendTestStore store = CreateStore();

        await using (SmokeContext seed = CreateClient(store))
        {
            seed.Blogs.Add(new Blog { Id = 1, Title = "alpha" });
            await seed.SaveChangesAsync();
        }

        await using SmokeContext context = CreateClient(store);

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Blogs.Where(b => ClientOnly.TitleIsLong(b.Title)).ToListAsync());

        Assert.Contains("could not be translated", thrown.Message);
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
