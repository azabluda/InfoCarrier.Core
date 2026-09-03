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
    /// <remarks>
    ///     <b>Every server here says its store is relational (#97), because it is SQLite.</b>
    ///     <c>InfoCarrierBackendTestStore</c> registers this for a fixture that asked for raw SQL,
    ///     and this class does not go through a fixture: it builds
    ///     <see cref="SharedTestStoreProperties" /> by hand and never sets
    ///     <c>ArbitrarySqlExecution</c>, so that registration does not reach it. Stated here rather
    ///     than in each test that needs it, so the two halves are configured in one place each --
    ///     the client's is on <see cref="CreateClient" />.
    ///     <para>
    ///         <b>It grants nothing.</b> Whether the server will run a string is still
    ///         <c>AddInfoCarrierArbitrarySqlExecution()</c>, which two tests add and the rest do
    ///         not; <c>A_FromSql_query_is_refused_...</c> and
    ///         <c>Granting_it_on_the_client_alone_...</c> pin both refusals with this in place.
    ///     </para>
    /// </remarks>
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
                OnAddServices = services => onAddServices?.Invoke(services) ?? services,
            });

    /// <remarks>
    ///     <b>Every client here says its backing store is relational (#97), and this class is why
    ///     that option exists.</b> It builds its client by hand rather than through
    ///     <c>InfoCarrierTestStoreFactory</c>, so there is no <c>IServiceCollection</c> to call
    ///     <c>AddInfoCarrierRelationalClient()</c> on — which is the shape most consumer
    ///     applications have. The instance comes from the caller for the same reason
    ///     <c>AllowTypes</c>'s types do: <c>InfoCarrier.Core</c> cannot name a relational type.
    /// </remarks>
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

    // R95. The raw-SQL gate (#60), in the same three shapes R85's allowed-types seam is pinned in,
    // plus one for the arguments.
    //
    // WHAT IS GRANTED IS ARBITRARY SQL EXECUTION, NOT A QUERY FEATURE, and the API names it that
    // way because `Sqlite/RawSqlExecutionProbeTest` (R94) measured what it means: one
    // `CommandText` runs every statement it contains, and an uncomposed `FromSqlRaw` reaches the
    // store unwrapped. There is no read-only subset to offer, so there is none in the API.
    //
    // These four also REPLACE the tripwire `FromSqlAssertions` describes -- "if `FromSql` is ever
    // supported, every one of them fails". It is supported now, on an opt-in a server has to make,
    // and the refusal that class asserts is still the default and is still asserted, here.
    [ConditionalFact]
    public async Task A_FromSql_query_is_refused_when_neither_side_grants_it()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using SqliteSmokeContext client = CreateClient(store);

        // The refusal names the node, which is exactly what `FromSqlAssertions` pins across the
        // inherited suites -- so this is the same refusal those tests assert, restated where the
        // gate lives. Default-deny, and the behaviour every caller had before the gate existed.
        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blogs.FromSqlRaw(@"SELECT * FROM ""Blogs""").CountAsync());

        Assert.Contains("FromSqlQueryRootExpression", refused.Message, StringComparison.Ordinal);
    }

    [ConditionalFact]
    public async Task A_FromSql_query_crosses_once_both_sides_grant_it()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync(
            services => services.AddInfoCarrierArbitrarySqlExecution());

        await using SqliteSmokeContext client = CreateClient(store, o => o.AllowArbitrarySqlExecution());

        // The store holds two blogs. THE `WHERE` IS THE POINT: before R75 a `FromSqlRaw` carrying
        // one came back as the whole table, because the wire's query-root node had no field for
        // the SQL and the subclass was shipped as its base. An answer of 2 here is that regression
        // returning, and it would otherwise look like an ordinary passing query.
        Assert.Equal(1, await client.Blogs.FromSqlRaw(@"SELECT * FROM ""Blogs"" WHERE ""Title"" = 'alpha'").CountAsync());
        Assert.Equal(2, await client.Blogs.FromSqlRaw(@"SELECT * FROM ""Blogs""").CountAsync());

        // And composed over, which is the shape EF wraps in a subquery.
        Blog[] composed = [.. await client.Blogs
            .FromSqlRaw(@"SELECT * FROM ""Blogs""")
            .Where(b => b.Title == "beta")
            .ToArrayAsync()];

        Assert.Equal("beta", Assert.Single(composed).Title);
    }

    [ConditionalFact]
    public async Task Granting_it_on_the_client_alone_is_refused_by_the_server()
    {
        // The half that makes the two registrations something other than duplication, and the one
        // that is the security boundary. The client now ships the query and the SERVER refuses to
        // run the string, which is what `docs/security-review.md` section 5a is about.
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using SqliteSmokeContext client = CreateClient(store, o => o.AllowArbitrarySqlExecution());

        // Raised on the SERVER and rehydrated here by `InfoCarrierFaultMapper`, which round-trips
        // an `InvalidOperationException` as itself -- so the exception TYPE says nothing about
        // which side refused, and the message is what does.
        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blogs.FromSqlRaw(@"SELECT * FROM ""Blogs""").CountAsync());

        Assert.Contains("does not permit a payload to carry SQL", refused.Message, StringComparison.Ordinal);
    }

    [ConditionalFact]
    public async Task FromSql_arguments_cross_as_values_and_are_bound_rather_than_interpolated()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync(
            services => services.AddInfoCarrierArbitrarySqlExecution());

        await using SqliteSmokeContext client = CreateClient(store, o => o.AllowArbitrarySqlExecution());

        // `{0}` becomes a `DbParameter` on the server, which is the only shape in which an
        // argument keeps its type across the wire. A quote in the value is the assertion that it
        // was bound and not spliced: interpolated into the text it would be a syntax error, and
        // matching zero rows through a parameter is the correct answer.
        Assert.Equal(
            1,
            await client.Blogs.FromSqlRaw(@"SELECT * FROM ""Blogs"" WHERE ""Title"" = {0}", "alpha").CountAsync());
        Assert.Equal(
            0,
            await client.Blogs.FromSqlRaw(@"SELECT * FROM ""Blogs"" WHERE ""Title"" = {0}", "al'pha").CountAsync());
    }

    // R89. Being NAMEABLE on the wire is not being SHIPPABLE, and conflating the two removed a
    // refusal. `QuerySplitter.ClientCodeFinder` refuses a method whose declaring type the
    // allowlist does not admit; R84 admitted the declaring type of every `HasDbFunction` mapping
    // so the call could be named, and for a function mapped as an INSTANCE method that type is
    // the caller's own `DbContext`. The clause then stopped firing while the call stayed
    // unshippable for a different reason -- its `Object` is a constant holding the live client
    // context, which no wire carries.
    //
    // What that cost was measured, not reasoned about: a boundary probe on
    // `UdfDbFunctionInfoCarrierTest.Scalar_Function_Where_Correlated_Instance` reported
    // `shippable=1` -- the bare query root -- and the client filtered the whole table. Across R84
    // the reasons diff shows 38 `TranslationFailed` refusals disappearing into 18 client
    // evaluations and 15 "no part of the query can be executed".
    //
    // `SqliteSmokeContext.TitleIsLong` is mapped with `HasDbFunction`, which is what puts this
    // context type on the allowlist -- the exact condition, reproduced without depending on the
    // inherited UDF class. `TitleIsLong` throws, so a regression arrives named in the assertion
    // message rather than as a green count.
    [ConditionalFact]
    public async Task A_predicate_calling_a_mapped_function_on_the_client_context_is_refused()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using SqliteSmokeContext client = CreateClient(store);

        InvalidOperationException refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Blogs.CountAsync(b => client.TitleIsLong(b.Title)));

        Assert.Contains("could not be translated", refused.Message, StringComparison.Ordinal);
    }

    // R91. D7's `RelationalDbFunctionAttributeConvention` row, settled by reading both models.
    //
    // `HasDbFunction` is a call the caller's own `OnModelCreating` makes, so both models get it.
    // `[DbFunction]` is picked up by a RELATIONAL convention, which only the server runs -- so the
    // server's model maps the method and the client's does not. R84's `ModelDbFunctions` reads the
    // client's model, so the allowlist never admits the declaring type and the call is refused
    // rather than translated.
    //
    // THE CALL STILL CROSSES, AND THAT WAS NOT THE PREDICTION. The models disagree, but what
    // decides the outcome is the ALLOWLIST, and `SqliteSmokeContext` is on it for an unrelated
    // reason -- `TitleIsLong`'s `HasDbFunction` mapping put it there. So the static call ships,
    // the server translates it against ITS model, and the only thing that fails is the store
    // lacking the function. That is the correct end-to-end behaviour; had this context declared
    // no mapped function at all, the same call would have been refused instead.
    //
    // Which is the third time in this session that "the allowlist admitted the type for an
    // unrelated reason" decided the behaviour -- R84, R89 and here. The type allowlist is load
    // bearing for far more than deserialization safety, and nothing says so where it is declared.
    [ConditionalFact]
    public async Task A_DbFunction_declared_by_attribute_reaches_the_servers_model_and_not_the_clients()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using SqliteSmokeContext client = CreateClient(store);
        using DbContext server = store.CreateDbContext();

        string[] onTheServer = [.. InfoCarrier.Core.Metadata.ModelDbFunctions.ForModel(server.Model)
            .Select(m => m.Name).Order()];
        string[] onTheClient = [.. InfoCarrier.Core.Metadata.ModelDbFunctions.ForModel(client.Model)
            .Select(m => m.Name).Order()];

        // The divergence, established before anything is concluded from it.
        Assert.Equal([nameof(SqliteSmokeContext.TitleIsLong), nameof(SqliteSmokeContext.TitleIsShort)], onTheServer);
        Assert.Equal([nameof(SqliteSmokeContext.TitleIsLong)], onTheClient);

        // And what it costs: nothing here. The call reaches the store as a function call, which is
        // what a server that maps it should produce. `TitleIsShort` throws when invoked, so this
        // could not be a client-side answer wearing a store-side message.
        InfoCarrierServerException reached = await Assert.ThrowsAsync<InfoCarrierServerException>(
            () => client.Blogs.CountAsync(b => SqliteSmokeContext.TitleIsShort(b.Title)));

        Assert.Contains("no such function: TitleIsShort", reached.Message, StringComparison.Ordinal);
    }

    // R91. D7's `TableSharingConcurrencyTokenConvention` row, settled by running it.
    //
    // Where entity types share a table and only some carry a concurrency token, that relational
    // convention gives the others a SHADOW token property mapped to the same column. This client
    // does not run it, so the server's model has a property the client's does not -- and the
    // property set is what `SaveChanges` sends. Nothing this repository inherits can reach the
    // shape: EF's only functional coverage is `OptimisticConcurrencySqlServerTest`, SQL Server's
    // own class rather than a specification base, over `rowversion`.
    //
    // TWO MEASURED ANSWERS, AND THE FIRST IS WHY THE SECOND WAS SO NEARLY MISSED.
    //
    // ONE: the convention needs BOTH `IsConcurrencyToken` AND `ValueGenerated.OnUpdate` --
    // `GetConcurrencyTokensMap.FindConcurrencyColumns` skips anything else. `ValueGenerated.OnUpdate`
    // on a token means `rowversion`, which SQLite does not have; an application-managed token, the
    // only kind this tier can express, never reaches the convention at all. So on every store this
    // repository tests, the convention CANNOT FIRE, and the client not running it is unobservable
    // by construction rather than by luck. The first version of this probe used a plain
    // `IsConcurrencyToken()` and passed while proving nothing -- the model assertions below are
    // what caught that.
    //
    // TWO: forced to fire, the divergence is real and costs nothing. The server's model gains the
    // synthesized property and the client's does not, and both halves of the split still round-trip
    // and save. The server applies its own model to entries the client sent by property NAME; a
    // property the client never mentions is simply not among them.
    //
    // `_TableSharingConcurrencyTokenConvention_` is a literal here on purpose: the constant is
    // private to EF, and this is a probe rather than a contract.
    [ConditionalFact]
    public async Task A_table_split_pair_whose_server_model_has_a_synthesized_concurrency_token_round_trips()
    {
        await using var store = new SqliteInfoCarrierBackendTestStore(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(TableSplitSmokeContext),
                OnModelCreating = (_, _) => { },
            });

        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(
                    new SharedRoot
                    {
                        Id = 1,
                        Name = "root",
                        Version = "v1",
                        Detail = new SharedDetail { Note = "first" },
                    });
                await context.SaveChangesAsync();
            });

        await using (var client = new TableSplitSmokeContext(
            new DbContextOptionsBuilder<TableSplitSmokeContext>().UseInfoCarrier(store).Options))
        {
            // ESTABLISH THAT THE DIVERGENCE IS REAL BEFORE CONCLUDING ANYTHING FROM A GREEN ROUND
            // TRIP. A convention that never fired and a divergence that costs nothing look
            // identical from outside, and CLAUDE.md names that mistake. The server's model must
            // have the synthesized token on `SharedDetail` and the client's must not; if EF ever
            // stops synthesizing it, this fails here rather than silently making the rest vacuous.
            const string synthesized = "_TableSharingConcurrencyTokenConvention_Version";

            using DbContext server = store.CreateDbContext();
            Assert.NotNull(server.Model.FindEntityType(typeof(SharedDetail))!.FindProperty(synthesized));
            Assert.Null(client.Model.FindEntityType(typeof(SharedDetail))!.FindProperty(synthesized));

            SharedDetail detail = await client.Details.SingleAsync();
            detail.Note = "second";
            Assert.Equal(1, await client.SaveChangesAsync());
        }

        await using (var client = new TableSplitSmokeContext(
            new DbContextOptionsBuilder<TableSplitSmokeContext>().UseInfoCarrier(store).Options))
        {
            SharedRoot root = await client.Roots.SingleAsync();
            root.Name = "renamed";
            Assert.Equal(1, await client.SaveChangesAsync());
        }

        await using (var client = new TableSplitSmokeContext(
            new DbContextOptionsBuilder<TableSplitSmokeContext>().UseInfoCarrier(store).Options))
        {
            Assert.Equal("second", (await client.Details.SingleAsync()).Note);
            Assert.Equal("renamed", (await client.Roots.SingleAsync()).Name);

            // `Version` is deliberately not asserted. `ValueGeneratedOnAddOrUpdate` makes EF omit
            // the property from the INSERT and expect the store to supply it, and SQLite supplies
            // nothing -- so it reads back null, on this wire and on a direct SQLite context alike.
            // That is the store's behaviour, not this provider's, and asserting it would pin the
            // wrong thing.
        }
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
