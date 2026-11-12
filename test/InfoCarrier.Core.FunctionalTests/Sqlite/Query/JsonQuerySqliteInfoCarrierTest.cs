// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestModels.JsonQuery;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Sdk;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>JsonQueryTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         A deep owned graph — reference and collection, two levels down, with inheritance, custom
///         naming, converters and a type-per-property entity — stored in JSON columns and queried
///         through. For this provider the JSON mapping is the backing store's business; what is
///         under test here is whether a shape that deep survives the projection split and comes
///         back materialized.
///     </para>
///     <para>
///         <b>Tier B</b>: EF ships <c>JsonQuerySqliteTest</c> and no InMemory counterpart, for the
///         obvious reason — there is no JSON column in a store that keeps live objects.
///     </para>
///     <para>
///         <b>The relational base, since R70.</b> It used to be the core one, with
///         <c>JsonQueryRelationalFixture</c>'s <c>ToJson()</c> mapping and three of its overrides
///         copied in by hand because the assembly holding it was not referenced. R1 referenced it,
///         and the re-parent deletes every one of those duplicates — this is the same payoff R66
///         and R69 collected. What is still copied is <c>JsonQuerySqliteFixture</c>'s ignores,
///         which are the <em>store's</em> statement rather than the relational base's: SQLite does
///         not map a collection of collections.
///     </para>
///     <para>
///         <b>Reaching the backend past a shadowed property.</b> <c>JsonQueryRelationalFixture</c>
///         declares <c>public new RelationalTestStore TestStore</c>, and every <em>read</em> of
///         that property throws on a store that is not relational — so
///         <c>Fixture.TestStore</c> stops compiling here, not merely failing at run time. This is
///         a shape ADR-013's 2026-08-30 amendment does not cover, and the way past it is
///         <see cref="JsonQuerySqliteInfoCarrierFixture.InfoCarrierTestStore" />: cast <c>this</c>
///         to the named base <em>inside</em> the fixture, where the shadow does not apply.
///     </para>
/// </remarks>
public class JsonQuerySqliteInfoCarrierTest(
    JsonQuerySqliteInfoCarrierTest.JsonQuerySqliteInfoCarrierFixture fixture)
    : JsonQueryRelationalTestBase<JsonQuerySqliteInfoCarrierTest.JsonQuerySqliteInfoCarrierFixture>(fixture)
{

    // EF Core 11 folded the standalone non-shared JSON/collection tests into QueryTestBase.
    private readonly NonSharedModelInfoCarrierHarness _nonShared = new(SqliteInfoCarrierTier.Instance);

    /// <inheritdoc />
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 779, 781,
        Justification = Upstream.GaveNoReason)]
    public override Task SelectMany_over_primitive_collection_nested_in_complex_collection_inside_json_column()
        => Assert.ThrowsAsync<InvalidOperationException>(
            base.SelectMany_over_primitive_collection_nested_in_complex_collection_inside_json_column);

    /// <inheritdoc />
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 458, 465,
        Justification = Upstream.GaveNoReason)]
    public override async Task
        Entity_including_collection_with_json_and_separate_json_projection_AsNoTrackingWithIdentityResolution(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.Entity_including_collection_with_json_and_separate_json_projection_AsNoTrackingWithIdentityResolution(async)))
            .Message);

    protected override ITestStoreFactory NonSharedTestStoreFactory => _nonShared.TestStoreFactory;

    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.DependencyInjection.IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true,
        bool skipValidation = false)
    {
        NonSharedFixture = null;
        _nonShared.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddNonSharedOptions);
        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider, skipValidation);
    }

    /// <summary>
    ///     Runs a seed statement against the backing store rather than through the wire.
    /// </summary>
    /// <remarks>
    ///     The client context these seeds are handed has no database at all, so
    ///     <c>Database.ExecuteSqlAsync</c> is not available on it — and would be the wrong thing
    ///     anyway: these rows are a statement about what the store already contains, not about
    ///     anything this provider should be asked to write.
    /// </remarks>
    private async Task ExecuteOnBackendAsync(FormattableString sql)
    {
        using DbContext backend = ((IInfoCarrierClientTestStore)NonSharedTestStore).Backend.CreateDbContext();
        await backend.Database.ExecuteSqlAsync(sql);
    }

    protected override async Task Seed21006(Context21006 context)
    {
        await base.Seed21006(context);

        // missing scalar on top level
        await ExecuteOnBackendAsync(
            $$$"""
INSERT INTO "Entities" ("Collection", "OptionalReference", "RequiredReference", "Id", "Name")
VALUES (
'[{"Text":"e2 c1","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e2 c1 c1"},{"DoB":"2000-01-01T00:00:00","Text":"e2 c1 c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e2 c1 nor"},"NestedRequiredReference":{"DoB":"2000-01-01T00:00:00","Text":"e2 c1 nrr"}},{"Text":"e2 c2","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e2 c2 c1"},{"DoB":"2000-01-01T00:00:00","Text":"e2 c2 c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e2 c2 nor"},"NestedRequiredReference":{"DoB":"2000-01-01T00:00:00","Text":"e2 c2 nrr"}}]',
'{"Text":"e2 or","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e2 or c1"},{"DoB":"2000-01-01T00:00:00","Text":"e2 or c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e2 or nor"},"NestedRequiredReference":{"DoB":"2000-01-01T00:00:00","Text":"e2 or nrr"}}',
'{"Text":"e2 rr","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e2 rr c1"},{"DoB":"2000-01-01T00:00:00","Text":"e2 rr c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e2 rr nor"},"NestedRequiredReference":{"DoB":"2000-01-01T00:00:00","Text":"e2 rr nrr"}}',
2,
'e2')
""");

        // missing scalar on nested level
        await ExecuteOnBackendAsync(
            $$$"""
INSERT INTO "Entities" ("Collection", "OptionalReference", "RequiredReference", "Id", "Name")
VALUES (
'[{"Number":7,"Text":"e3 c1","NestedCollection":[{"Text":"e3 c1 c1"},{"Text":"e3 c1 c2"}],"NestedOptionalReference":{"Text":"e3 c1 nor"},"NestedRequiredReference":{"Text":"e3 c1 nrr"}},{"Number":7,"Text":"e3 c2","NestedCollection":[{"Text":"e3 c2 c1"},{"Text":"e3 c2 c2"}],"NestedOptionalReference":{"Text":"e3 c2 nor"},"NestedRequiredReference":{"Text":"e3 c2 nrr"}}]',
'{"Number":7,"Text":"e3 or","NestedCollection":[{"Text":"e3 or c1"},{"Text":"e3 or c2"}],"NestedOptionalReference":{"Text":"e3 or nor"},"NestedRequiredReference":{"Text":"e3 or nrr"}}',
'{"Number":7,"Text":"e3 rr","NestedCollection":[{"Text":"e3 rr c1"},{"Text":"e3 rr c2"}],"NestedOptionalReference":{"Text":"e3 rr nor"},"NestedRequiredReference":{"Text":"e3 rr nrr"}}',
3,
'e3')
""");

        // null scalar on top level
        await ExecuteOnBackendAsync(
            $$$"""
INSERT INTO [Entities] ("Collection", "OptionalReference", "RequiredReference", "Id", "Name")
VALUES (
'[{"Number":null,"Text":"e4 c1","NestedCollection":[{"Text":"e4 c1 c1"},{"Text":"e4 c1 c2"}],"NestedOptionalReference":{"Text":"e4 c1 nor"},"NestedRequiredReference":{"Text":"e4 c1 nrr"}},{"Number":null,"Text":"e4 c2","NestedCollection":[{"Text":"e4 c2 c1"},{"Text":"e4 c2 c2"}],"NestedOptionalReference":{"Text":"e4 c2 nor"},"NestedRequiredReference":{"Text":"e4 c2 nrr"}}]',
'{"Number":null,"Text":"e4 or","NestedCollection":[{"Text":"e4 or c1"},{"Text":"e4 or c2"}],"NestedOptionalReference":{"Text":"e4 or nor"},"NestedRequiredReference":{"Text":"e4 or nrr"}}',
'{"Number":null,"Text":"e4 rr","NestedCollection":[{"Text":"e4 rr c1"},{"Text":"e4 rr c2"}],"NestedOptionalReference":{"Text":"e4 rr nor"},"NestedRequiredReference":{"Text":"e4 rr nrr"}}',
4,
'e4')
""");

        // missing required navigation
        await ExecuteOnBackendAsync(
            $$$"""
INSERT INTO "Entities" ("Collection", "OptionalReference", "RequiredReference", "Id", "Name")
VALUES (
'[{"Number":7,"Text":"e5 c1","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e5 c1 c1"},{"DoB":"2000-01-01T00:00:00","Text":"e5 c1 c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e5 c1 nor"}},{"Number":7,"Text":"e5 c2","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e5 c2 c1"},{"DoB":"2000-01-01T00:00:00","Text":"e5 c2 c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e5 c2 nor"}}]',
'{"Number":7,"Text":"e5 or","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e5 or c1"},{"DoB":"2000-01-01T00:00:00","Text":"e5 or c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e5 or nor"}}',
'{"Number":7,"Text":"e5 rr","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e5 rr c1"},{"DoB":"2000-01-01T00:00:00","Text":"e5 rr c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e5 rr nor"}}',
5,
'e5')
""");

        // null required navigation
        await ExecuteOnBackendAsync(
            $$$"""
INSERT INTO "Entities" ("Collection", "OptionalReference", "RequiredReference", "Id", "Name")
VALUES (
'[{"Number":7,"Text":"e6 c1","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e6 c1 c1"},{"DoB":"2000-01-01T00:00:00","Text":"e6 c1 c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e6 c1 nor"},"NestedRequiredReference":null},{"Number":7,"Text":"e6 c2","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e6 c2 c1"},{"DoB":"2000-01-01T00:00:00","Text":"e6 c2 c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e6 c2 nor"},"NestedRequiredReference":null}]',
'{"Number":7,"Text":"e6 or","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e6 or c1"},{"DoB":"2000-01-01T00:00:00","Text":"e6 or c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e6 or nor"},"NestedRequiredReference":null}',
'{"Number":7,"Text":"e6 rr","NestedCollection":[{"DoB":"2000-01-01T00:00:00","Text":"e6 rr c1"},{"DoB":"2000-01-01T00:00:00","Text":"e6 rr c2"}],"NestedOptionalReference":{"DoB":"2000-01-01T00:00:00","Text":"e6 rr nor"},"NestedRequiredReference":null}',
6,
'e6')
""");
    }

    protected override async Task Seed29219(DbContext ctx)
    {
        await base.Seed29219(ctx);

        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO "Entities" ("Id", "Reference", "Collection")
VALUES(3, '{ "NonNullableScalar" : 30 }', '[{ "NonNullableScalar" : 10001 }]')
""");
    }

    protected override async Task Seed30028(DbContext ctx)
    {
        // complete
        await ExecuteOnBackendAsync(
            $$$$"""
INSERT INTO "Entities" ("Id", "Json")
VALUES(
1,
'{"RootName":"e1","Collection":[{"BranchName":"e1 c1","Nested":{"LeafName":"e1 c1 l"}},{"BranchName":"e1 c2","Nested":{"LeafName":"e1 c2 l"}}],"OptionalReference":{"BranchName":"e1 or","Nested":{"LeafName":"e1 or l"}},"RequiredReference":{"BranchName":"e1 rr","Nested":{"LeafName":"e1 rr l"}}}')
""");

        // missing collection
        await ExecuteOnBackendAsync(
            $$$$"""
INSERT INTO "Entities" ("Id", "Json")
VALUES(
2,
'{"RootName":"e2","OptionalReference":{"BranchName":"e2 or","Nested":{"LeafName":"e2 or l"}},"RequiredReference":{"BranchName":"e2 rr","Nested":{"LeafName":"e2 rr l"}}}')
""");

        // missing optional reference
        await ExecuteOnBackendAsync(
            $$$$"""
INSERT INTO "Entities" ("Id", "Json")
VALUES(
3,
'{"RootName":"e3","Collection":[{"BranchName":"e3 c1","Nested":{"LeafName":"e3 c1 l"}},{"BranchName":"e3 c2","Nested":{"LeafName":"e3 c2 l"}}],"RequiredReference":{"BranchName":"e3 rr","Nested":{"LeafName":"e3 rr l"}}}')
""");

        // missing required reference
        await ExecuteOnBackendAsync(
            $$$$"""
INSERT INTO "Entities" ("Id", "Json")
VALUES(
4,
'{"RootName":"e4","Collection":[{"BranchName":"e4 c1","Nested":{"LeafName":"e4 c1 l"}},{"BranchName":"e4 c2","Nested":{"LeafName":"e4 c2 l"}}],"OptionalReference":{"BranchName":"e4 or","Nested":{"LeafName":"e4 or l"}}}')
""");
    }

    protected override async Task Seed33046(DbContext ctx)
        => await ExecuteOnBackendAsync(
            $$"""
INSERT INTO "Reviews" ("Rounds", "Id")
VALUES('[{"RoundNumber":11,"SubRounds":[{"SubRoundNumber":111},{"SubRoundNumber":112}]}]', 1)
""");

    protected override async Task Seed34960(Context34960 ctx)
    {
        await base.Seed34960(ctx);

        // JSON nulls
        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO "Entities" ("Collection", "Reference", "Id")
VALUES(
'null',
'null',
4)
""");

        // JSON object where collection should be
        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO "Junk" ("Collection", "Reference", "Id")
VALUES(
'{ "DoB":"2000-01-01T00:00:00","Text":"junk" }',
NULL,
1)
""");

        // JSON array where entity should be
        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO "Junk" ("Collection", "Reference", "Id")
VALUES(
NULL,
'[{ "DoB":"2000-01-01T00:00:00","Text":"junk" }]',
2)
""");
    }

    protected override Task SeedJunkInJson(DbContext ctx)
        => ExecuteOnBackendAsync(
            $$$"""
INSERT INTO "Entities" ("Collection", "CollectionWithCtor", "Reference", "ReferenceWithCtor", "Id")
VALUES(
'[{"JunkReference":{"Something":"SomeValue" },"Name":"c11","JunkProperty1":50,"Number":11.5,"JunkCollection1":[],"JunkCollection2":[{"Foo":"junk value"}],"NestedCollection":[{"DoB":"2002-04-01T00:00:00","DummyProp":"Dummy value"},{"DoB":"2002-04-02T00:00:00","DummyReference":{"Foo":5}}],"NestedReference":{"DoB":"2002-03-01T00:00:00"}},{"Name":"c12","Number":12.5,"NestedCollection":[{"DoB":"2002-06-01T00:00:00"},{"DoB":"2002-06-02T00:00:00"}],"NestedDummy":59,"NestedReference":{"DoB":"2002-05-01T00:00:00"}}]',
'[{"MyBool":true,"Name":"c11 ctor","JunkReference":{"Something":"SomeValue","JunkCollection":[{"Foo":"junk value"}]},"NestedCollection":[{"DoB":"2002-08-01T00:00:00"},{"DoB":"2002-08-02T00:00:00"}],"NestedReference":{"DoB":"2002-07-01T00:00:00"}},{"MyBool":false,"Name":"c12 ctor","NestedCollection":[{"DoB":"2002-10-01T00:00:00"},{"DoB":"2002-10-02T00:00:00"}],"JunkCollection":[{"Foo":"junk value"}],"NestedReference":{"DoB":"2002-09-01T00:00:00"}}]',
'{"Name":"r1","JunkCollection":[{"Foo":"junk value"}],"JunkReference":{"Something":"SomeValue" },"Number":1.5,"NestedCollection":[{"DoB":"2000-02-01T00:00:00","JunkReference":{"Something":"SomeValue"}},{"DoB":"2000-02-02T00:00:00"}],"NestedReference":{"DoB":"2000-01-01T00:00:00"}}',
'{"MyBool":true,"JunkCollection":[{"Foo":"junk value"}],"Name":"r1 ctor","JunkReference":{"Something":"SomeValue" },"NestedCollection":[{"DoB":"2001-02-01T00:00:00"},{"DoB":"2001-02-02T00:00:00"}],"NestedReference":{"JunkCollection":[{"Foo":"junk value"}],"DoB":"2001-01-01T00:00:00"}}',
1)
""");

    protected override Task SeedTrickyBuffering(DbContext ctx)
        => ExecuteOnBackendAsync(
            $$$"""
INSERT INTO "Entities" ("Reference", "Id")
VALUES(
'{"Name": "r1", "Number": 7, "JunkReference":{"Something": "SomeValue" }, "JunkCollection": [{"Foo": "junk value"}], "NestedReference": {"DoB": "2000-01-01T00:00:00"}, "NestedCollection": [{"DoB": "2000-02-01T00:00:00", "JunkReference": {"Something": "SomeValue"}}, {"DoB": "2000-02-02T00:00:00"}]}',1)
""");

    protected override Task SeedShadowProperties(DbContext ctx)
        => ExecuteOnBackendAsync(
            $$"""
INSERT INTO "Entities" ("Collection", "CollectionWithCtor", "Reference", "ReferenceWithCtor", "Id", "Name")
VALUES(
'[{"Name":"e1_c1","ShadowDouble":5.5},{"ShadowDouble":20.5,"Name":"e1_c2"}]',
'[{"Name":"e1_c1 ctor","ShadowNullableByte":6},{"ShadowNullableByte":null,"Name":"e1_c2 ctor"}]',
'{"Name":"e1_r", "ShadowString":"Foo"}',
'{"ShadowInt":143,"Name":"e1_r ctor"}',
1,
'e1')
""");

    protected override async Task SeedNotICollection(DbContext ctx)
    {
        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO "Entities" ("Json", "Id")
VALUES(
'{"Collection":[{"Bar":11,"Foo":"c11"},{"Bar":12,"Foo":"c12"},{"Bar":13,"Foo":"c13"}]}',
1)
""");

        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO "Entities" ("Json", "Id")
VALUES(
'{"Collection":[{"Bar":21,"Foo":"c21"},{"Bar":22,"Foo":"c22"}]}',
2)
""");
    }

    protected override async Task SeedBadJsonProperties(ContextBadJsonProperties ctx)
    {
        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO Entities (Id, Scenario, OptionalReference, RequiredReference, Collection)
VALUES(
1,
'baseline',
'{"NestedOptional": { "Text":"or no" }, "NestedRequired": { "Text":"or nr" }, "NestedCollection": [ { "Text":"or nc 1" }, { "Text":"or nc 2" } ] }',
'{"NestedOptional": { "Text":"rr no" }, "NestedRequired": { "Text":"rr nr" }, "NestedCollection": [ { "Text":"rr nc 1" }, { "Text":"rr nc 2" } ] }',
'[
{"NestedOptional": { "Text":"c 1 no" }, "NestedRequired": { "Text":"c 1 nr" }, "NestedCollection": [ { "Text":"c 1 nc 1" }, { "Text":"c 1 nc 2" } ] },
{"NestedOptional": { "Text":"c 2 no" }, "NestedRequired": { "Text":"c 2 nr" }, "NestedCollection": [ { "Text":"c 2 nc 1" }, { "Text":"c 2 nc 2" } ] }
]')
""");

        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO Entities (Id, Scenario, OptionalReference, RequiredReference, Collection)
VALUES(
2,
'duplicated navigations',
'{"NestedOptional": { "Text":"or no" }, "NestedOptional": { "Text":"or no dupnav" }, "NestedRequired": { "Text":"or nr" }, "NestedCollection": [ { "Text":"or nc 1" }, { "Text":"or nc 2" } ], "NestedCollection": [ { "Text":"or nc 1 dupnav" }, { "Text":"or nc 2 dupnav" } ], "NestedRequired": { "Text":"or nr dupnav" } }',
'{"NestedOptional": { "Text":"rr no" }, "NestedOptional": { "Text":"rr no dupnav" }, "NestedRequired": { "Text":"rr nr" }, "NestedCollection": [ { "Text":"rr nc 1" }, { "Text":"rr nc 2" } ], "NestedCollection": [ { "Text":"rr nc 1 dupnav" }, { "Text":"rr nc 2 dupnav" } ], "NestedRequired": { "Text":"rr nr dupnav" } }',
'[
{"NestedOptional": { "Text":"c 1 no" }, "NestedOptional": { "Text":"c 1 no dupnav" }, "NestedRequired": { "Text":"c 1 nr" }, "NestedCollection": [ { "Text":"c 1 nc 1" }, { "Text":"c 1 nc 2" } ], "NestedCollection": [ { "Text":"c 1 nc 1 dupnav" }, { "Text":"c 1 nc 2 dupnav" } ], "NestedRequired": { "Text":"c 1 nr dupnav" } },
{"NestedOptional": { "Text":"c 2 no" }, "NestedOptional": { "Text":"c 2 no dupnav" }, "NestedRequired": { "Text":"c 2 nr" }, "NestedCollection": [ { "Text":"c 2 nc 1" }, { "Text":"c 2 nc 2" } ], "NestedCollection": [ { "Text":"c 2 nc 1 dupnav" }, { "Text":"c 2 nc 2 dupnav" } ], "NestedRequired": { "Text":"c 2 nr dupnav" } }
]')
""");

        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO Entities (Id, Scenario, OptionalReference, RequiredReference, Collection)
VALUES(
3,
'duplicated scalars',
'{"NestedOptional": { "Text":"or no", "Text":"or no dupprop" }, "NestedRequired": { "Text":"or nr", "Text":"or nr dupprop" }, "NestedCollection": [ { "Text":"or nc 1", "Text":"or nc 1 dupprop" }, { "Text":"or nc 2", "Text":"or nc 2 dupprop" } ] }',
'{"NestedOptional": { "Text":"rr no", "Text":"rr no dupprop" }, "NestedRequired": { "Text":"rr nr", "Text":"rr nr dupprop" }, "NestedCollection": [ { "Text":"rr nc 1", "Text":"rr nc 1 dupprop" }, { "Text":"rr nc 2", "Text":"rr nc 2 dupprop" } ] }',
'[
{"NestedOptional": { "Text":"c 1 no", "Text":"c 1 no dupprop" }, "NestedRequired": { "Text":"c 1 nr", "Text":"c 1 nr dupprop" }, "NestedCollection": [ { "Text":"c 1 nc 1", "Text":"c 1 nc 1 dupprop" }, { "Text":"c 1 nc 2", "Text":"c 1 nc 2 dupprop" } ] },
{"NestedOptional": { "Text":"c 2 no", "Text":"c 2 no dupprop" }, "NestedRequired": { "Text":"c 2 nr", "Text":"c 2 nr dupprop" }, "NestedCollection": [ { "Text":"c 2 nc 1", "Text":"c 2 nc 1 dupprop" }, { "Text":"c 2 nc 2", "Text":"c 2 nc 2 dupprop" } ] }
]')
""");

        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO Entities (Id, Scenario, OptionalReference, RequiredReference, Collection)
VALUES(
4,
'empty navigation property names',
'{"": { "Text":"or no" }, "": { "Text":"or nr" }, "": [ { "Text":"or nc 1" }, { "Text":"or nc 2" } ] }',
'{"": { "Text":"rr no" }, "": { "Text":"rr nr" }, "": [ { "Text":"rr nc 1" }, { "Text":"rr nc 2" } ] }',
'[
{"": { "Text":"c 1 no" }, "": { "Text":"c 1 nr" }, "": [ { "Text":"c 1 nc 1" }, { "Text":"c 1 nc 2" } ] },
{"": { "Text":"c 2 no" }, "": { "Text":"c 2 nr" }, "": [ { "Text":"c 2 nc 1" }, { "Text":"c 2 nc 2" } ] }
]')
""");

        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO Entities (Id, Scenario, OptionalReference, RequiredReference, Collection)
VALUES(
5,
'empty scalar property names',
'{"NestedOptional": { "":"or no" }, "NestedRequired": { "":"or nr" }, "NestedCollection": [ { "":"or nc 1" }, { "":"or nc 2" } ] }',
'{"NestedOptional": { "":"rr no" }, "NestedRequired": { "":"rr nr" }, "NestedCollection": [ { "":"rr nc 1" }, { "":"rr nc 2" } ] }',
'[
{"NestedOptional": { "":"c 1 no" }, "NestedRequired": { "":"c 1 nr" }, "NestedCollection": [ { "":"c 1 nc 1" }, { "":"c 1 nc 2" } ] },
{"NestedOptional": { "":"c 2 no" }, "NestedRequired": { "":"c 2 nr" }, "NestedCollection": [ { "":"c 2 nc 1" }, { "":"c 2 nc 2" } ] }
]')
""");

        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO Entities (Id, Scenario, OptionalReference, RequiredReference, Collection)
VALUES(
10,
'null navigation property names',
'{null: { "Text":"or no" }, null: { "Text":"or nr" }, null: [ { "Text":"or nc 1" }, { "Text":"or nc 2" } ] }',
'{null: { "Text":"rr no" }, null: { "Text":"rr nr" }, null: [ { "Text":"rr nc 1" }, { "Text":"rr nc 2" } ] }',
'[
{null: { "Text":"c 1 no" }, null: { "Text":"c 1 nr" }, null: [ { "Text":"c 1 nc 1" }, { "Text":"c 1 nc 2" } ] },
{null: { "Text":"c 2 no" }, null: { "Text":"c 2 nr" }, null: [ { "Text":"c 2 nc 1" }, { "Text":"c 2 nc 2" } ] }
]')
""");

        await ExecuteOnBackendAsync(
            $$"""
INSERT INTO Entities (Id, Scenario, OptionalReference, RequiredReference, Collection)
VALUES(
11,
'null scalar property names',
'{"NestedOptional": { null:"or no", "Text":"or no nonnull" }, "NestedRequired": { null:"or nr", "Text":"or nr nonnull" }, "NestedCollection": [ { null:"or nc 1", "Text":"or nc 1 nonnull" }, { null:"or nc 2", "Text":"or nc 2 nonnull" } ] }',
'{"NestedOptional": { null:"rr no", "Text":"rr no nonnull" }, "NestedRequired": { null:"rr nr", "Text":"rr nr nonnull" }, "NestedCollection": [ { null:"rr nc 1", "Text":"rr nc 1 nonnull" }, { null:"rr nc 2", "Text":"rr nc 2 nonnull" } ] }',
'[
{"NestedOptional": { null:"c 1 no", "Text":"c 1 no nonnull" }, "NestedRequired": { null:"c 1 nr", "Text":"c 1 nr nonnull" }, "NestedCollection": [ { null:"c 1 nc 1", "Text":"c 1 nc 1 nonnull" }, { null:"c 1 nc 2", "Text":"c 1 nc 2 nonnull" } ] },
{"NestedOptional": { null:"c 2 no", "Text":"c 2 no nonnull" }, "NestedRequired": { null:"c 2 nr", "Text":"c 2 nr nonnull" }, "NestedCollection": [ { null:"c 2 nc 1", "Text":"c 2 nc 1 nonnull" }, { null:"c 2 nc 2", "Text":"c 2 nc 2 nonnull" } ] }
]')
""");
    }


    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 380, 390,
        Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(8,
        Justification = "The upstream helper is private and cannot be admitted on the server. Public shared helpers require registration on both ends.",
        Deviation = DeviationKind.AnswerNotRefusal,
        DeviationNote = "The private index executes locally after the JSON collection is materialized; own tests pin this behavior.")]
    public override Task Json_collection_index_in_projection_using_untranslatable_client_method(bool async)
        => base.Json_collection_index_in_projection_using_untranslatable_client_method(async);

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 392, 402,
        Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(8,
        Justification = "The upstream helper is private and cannot be admitted on the server. Public shared helpers require registration on both ends.",
        Deviation = DeviationKind.AnswerNotRefusal,
        DeviationNote = "The private index executes locally after the nested JSON collection is materialized; own tests pin this behavior.")]
    public override Task Json_collection_index_in_projection_using_untranslatable_client_method2(bool async)
        => base.Json_collection_index_in_projection_using_untranslatable_client_method2(async);

    /// <summary>
    ///     The seventeen overrides below are EF's own, from <c>JsonQuerySqliteTest</c>.
    /// </summary>
    /// <remarks>
    ///     Each is a query that now reaches SQL and asks SQLite for <c>APPLY</c>, which it does not
    ///     have — convergence with the reference provider rather than a defect of this one.
    ///     <para>
    ///         EF overrides seven more the same way and they are deliberately <em>not</em> taken:
    ///         they do not fail here for that reason, and an override whose reason is not ours
    ///         hides the real one (A63 adopted eight such and all eight failed with "Exception type
    ///         was not an exact match").
    ///     </para>
    ///     <para>
    ///         This paragraph used to end by naming
    ///         <c>Json_nested_collection_anonymous_projection_of_primitives_in_projection_NoTrackingWithIdentityResolution</c>
    ///         as one that <em>"raises <c>APPLY</c> here and EF does not override"</em>. <b>EF does
    ///         override it</b>, in this same file of EF's, and it is now the eighteenth below
    ///         (C96). Seventeen became eighteen by grepping rather than by re-reading the note.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     The two models agree about what identifies an element of a JSON-mapped owned
    ///     collection (B12, C80).
    /// </summary>
    /// <remarks>
    ///     Stated on the model rather than on a query result, because that is where the defect
    ///     lived and because it is the one thing the 36 query tests below all depend on. A JSON
    ///     document carries no key for its array elements, so every store synthesizes an ordinal;
    ///     the client kept the CLR `Id` instead, which the document does not contain, so every
    ///     element of every owner shared one key and EF's fixup gave each of them to all of them.
    /// </remarks>
    [Fact]
    public void The_two_models_agree_on_the_key_of_every_JSON_mapped_owned_collection()
    {
        using DbContext client = Fixture.CreateContext();
        using DbContext server = Fixture.InfoCarrierTestStore.Backend.CreateDbContext();

        var compared = 0;

        foreach (IEntityType clientType in client.Model.GetEntityTypes())
        {
            if (clientType.FindOwnership() is not { IsUnique: false }
                || clientType.GetContainerColumnName() is null)
            {
                continue;
            }

            IEntityType serverType = Assert.IsAssignableFrom<IEntityType>(
                server.Model.FindEntityType(clientType.Name));

            Assert.Equal(
                serverType.FindPrimaryKey()!.Properties.Select(p => p.Name),
                clientType.FindPrimaryKey()!.Properties.Select(p => p.Name));

            compared++;
        }

        // The corpus really does contain them — a silent zero would assert nothing at all.
        Assert.Equal(18, compared);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own <c>JsonQuerySqliteTest</c> override, verbatim: it asserts that the base's
    ///     comparison <em>fails</em> on this store, under EF issue #33522. A <c>byte[]</c> inside a
    ///     JSON document is not comparable by value in SQLite, so the query returns both rows where
    ///     the base expects one, and `EqualException` is what the base then throws.
    ///     <para>
    ///         Adopted rather than diagnosed (A63). C84 had this pair listed as the last two
    ///         <em>unexplained wrong answers</em> in the suite, which it never was — the probe put
    ///         the query wholly on the server (<c>wholly=True shippable=1</c>), and EF's suite had
    ///         the reason written down all along. **Grep EF's SQLite suite before calling a
    ///         `Values differ` on Tier B a wrong answer**; that is a standing rule in CLAUDE.md and
    ///         it was not applied here until after the ranking was published.
    ///     </para>
    /// </remarks>
    [StoreIssue(
        IssueTracker.EfCore, 33522,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 259, 261,
        Justification = "#33522")]
    public override Task Json_predicate_on_byte_array(bool async)
        => Assert.ThrowsAsync<EqualException>(() => base.Json_predicate_on_byte_array(async));

    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 323, 327,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_branch_collection_distinct_and_other_collection(bool async)
        => await AssertApplyNotSupported(() => base.Json_branch_collection_distinct_and_other_collection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 284, 289,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_Select_entity_in_anonymous_object_ElementAt(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_Select_entity_in_anonymous_object_ElementAt(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 180, 185,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_Select_entity_with_initializer_ElementAt(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_Select_entity_with_initializer_ElementAt(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 305, 309,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_distinct_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_distinct_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 311, 315,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_filter_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_filter_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 263, 268,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_in_projection_with_anonymous_projection_of_scalars(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_in_projection_with_anonymous_projection_of_scalars(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 277, 282,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_primitive_arrays(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_primitive_arrays(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 270, 275,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_scalars(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_scalars(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 317, 321,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_leaf_filter_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_leaf_filter_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 367, 378,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_of_primitives_SelectMany(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_of_primitives_SelectMany(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 341, 352,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_skip_take_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_skip_take_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 291, 296,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_skip_take_in_projection_project_into_anonymous_type(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_skip_take_in_projection_project_into_anonymous_type(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 298, 303,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_skip_take_in_projection_with_json_reference_access_as_final_operation(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_skip_take_in_projection_with_json_reference_access_as_final_operation(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 329, 333,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_leaf_collection_distinct_and_other_collection(bool async)
        => await AssertApplyNotSupported(() => base.Json_leaf_collection_distinct_and_other_collection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 335, 339,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_multiple_collection_projections(bool async)
        => await AssertApplyNotSupported(() => base.Json_multiple_collection_projections(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 354, 359,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_nested_collection_anonymous_projection_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_nested_collection_anonymous_projection_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 361, 365,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_nested_collection_filter_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_nested_collection_filter_in_projection(async));

    /// <summary>
    ///     The eighteenth, and the standing note above said EF did not override it (C96).
    /// </summary>
    /// <remarks>
    ///     It does, in the same file as the other seventeen —
    ///     <c>JsonQuerySqliteTest.cs</c>, <c>Assert.Equal(SqliteStrings.ApplyNotSupported, …)</c> —
    ///     and this provider raises that message character for character. The claim that it did not
    ///     was carried through C77 as a classification (*"SQLite has no APPLY"*) rather than
    ///     checked, which is the one thing CLAUDE.md says to do before calling a Tier B failure
    ///     ours. **Age is not evidence**, and neither is a note.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 437, 443,
        Justification = Upstream.GaveNoReason)]
    public override async Task Json_nested_collection_anonymous_projection_of_primitives_in_projection_NoTrackingWithIdentityResolution(
        bool async)
        => await AssertApplyNotSupported(
            () => base.Json_nested_collection_anonymous_projection_of_primitives_in_projection_NoTrackingWithIdentityResolution(async));

    /// <summary>
    ///     Four more of the same, arriving with the relational base (R70).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each asks SQLite for <c>APPLY</c> before it can reach the check the base is testing,
    ///         so the base's expected message —
    ///         <c>RelationalStrings.JsonProjectingQueryableOperationNoTrackingWithIdentityResolution</c>
    ///         — never gets raised. <b>EF's own <c>JsonQuerySqliteTest</c> overrides all four</b>,
    ///         and says why on the first of them: <i>"Sqlit throws APPLY error, but base expects
    ///         different exception"</i>. That makes these convergence with the reference provider
    ///         rather than a defect of this one, which is the standing test in <c>CLAUDE.md</c> for
    ///         a newly-red SQLite test.
    ///     </para>
    ///     <para>
    ///         <b>EF's form is taken verbatim, and the stronger one above was tried first and
    ///         cannot work here.</b> The eighteen overrides above wrap <c>base</c> in
    ///         <see cref="AssertApplyNotSupported" />, which asserts the refusal rather than
    ///         swallowing it. That idiom does not transfer to these four, because <b>these base
    ///         methods catch the <see cref="InvalidOperationException" /> themselves</b> and
    ///         compare its message — so what escapes <c>base</c> is an
    ///         <c>Xunit.Sdk.EqualException</c>, and the wrapper fails with <i>"Exception type was
    ///         not an exact match"</i>. <b>That is A63's shape exactly, reproduced by measuring
    ///         rather than reasoned about</b>: an override whose reason is not the base's hides the
    ///         real one. Asserting the APPLY refusal here would mean re-writing each of the four
    ///         queries outside the base, which pins this file to EF's query text.
    ///     </para>
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 455, 456,
        Justification = Upstream.GaveNoReason,
        Skip = true)]
    public override Task Json_projection_using_queryable_methods_on_top_of_JSON_collection_AsNoTrackingWithIdentityResolution(
        bool async)
        => Task.CompletedTask;

    /// <inheritdoc cref="Json_projection_using_queryable_methods_on_top_of_JSON_collection_AsNoTrackingWithIdentityResolution" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 452, 453,
        Justification = Upstream.GaveNoReason,
        Skip = true)]
    public override Task Json_nested_collection_anonymous_projection_in_projection_NoTrackingWithIdentityResolution(bool async)
        => Task.CompletedTask;

    /// <inheritdoc cref="Json_projection_using_queryable_methods_on_top_of_JSON_collection_AsNoTrackingWithIdentityResolution" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 445, 447,
        Justification = "Sqlit throws APPLY error, but base expects different exception",
        Skip = true)]
    public override Task Json_branch_collection_distinct_and_other_collection_AsNoTrackingWithIdentityResolution(bool async)
        => Task.CompletedTask;

    /// <inheritdoc cref="Json_projection_using_queryable_methods_on_top_of_JSON_collection_AsNoTrackingWithIdentityResolution" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/JsonQuerySqliteTest.cs", 449, 450,
        Justification = Upstream.GaveNoReason,
        Skip = true)]
    public override Task Json_collection_SelectMany_AsNoTrackingWithIdentityResolution(bool async)
        => Task.CompletedTask;

    // The three `Project_json_*_tracking_query_fails` overrides that used to sit here are gone:
    // `JsonQueryRelationalTestBase` declares all three, with the same assertion, and R70's
    // re-parent inherits them. They had been mirrored by hand only because the assembly holding
    // that base was not referenced before R1. `OwnsMany_correlated_projection` was left red here as
    // raising the same refusal with no EF override; it is not red on 2026-09-15.

    private static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);

    public class JsonQuerySqliteInfoCarrierFixture : JsonQueryRelationalFixture
    {
        /// <summary>
        ///     The wire's own store, reached past the shadowed <c>TestStore</c> property.
        /// </summary>
        /// <remarks>
        ///     <c>JsonQueryRelationalFixture</c> declares
        ///     <c>public new RelationalTestStore TestStore =&gt; (RelationalTestStore)base.TestStore</c>.
        ///     That hides the inherited property rather than overriding it, so from outside the
        ///     fixture the name resolves to the relational one and the cast throws for a store that
        ///     is not relational — <b>a compile error here, not a run-time one</b>, because the
        ///     shadowing type is unrelated to <see cref="TestUtilities.IInfoCarrierClientTestStore" />.
        ///     Casting <c>this</c> to the base that declares the original property reaches past the
        ///     shadow, and the type argument is the one
        ///     <c>JsonQueryFixtureBase</c> supplies — <c>JsonQueryContext</c>, not the
        ///     <c>PoolableDbContext</c> that the same trick needs for the owned-query fixtures.
        /// </remarks>
        public IInfoCarrierClientTestStore InfoCarrierTestStore
            => (IInfoCarrierClientTestStore)((SharedStoreFixtureBase<JsonQueryContext>)this).TestStore;

        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "JsonQuerySqliteInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions,
                relationalClientStore: true,
                arbitrarySqlExecution: true);

        /// <inheritdoc />
        /// <remarks>
        ///     <c>JsonQuerySqliteFixture</c>'s ignores, and only those — SQLite does not map a
        ///     collection of collections. The <c>ToJson()</c> mapping that used to be copied in
        ///     above them now arrives from <c>JsonQueryRelationalFixture</c> (R70), which is where
        ///     it was always mirrored from.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<JsonEntityAllTypes>(b =>
            {
                b.Ignore(e => e.TestInt64CollectionCollection);
                b.Ignore(e => e.TestDoubleCollectionCollection);
                b.Ignore(e => e.TestSingleCollectionCollection);
                b.Ignore(e => e.TestBooleanCollectionCollection);
                b.Ignore(e => e.TestCharacterCollectionCollection);
                b.Ignore(e => e.TestDefaultStringCollectionCollection);
                b.Ignore(e => e.TestMaxLengthStringCollectionCollection);
                b.Ignore(e => e.TestInt16CollectionCollection);
                b.Ignore(e => e.TestInt32CollectionCollection);
                b.Ignore(e => e.TestNullableEnumWithIntConverterCollectionCollection);
                b.Ignore(e => e.TestNullableInt32CollectionCollection);
                b.Ignore(e => e.TestNullableEnumCollectionCollection);

                b.OwnsOne(
                    e => e.Reference, b =>
                    {
                        b.Ignore(e => e.TestInt64CollectionCollection);
                        b.Ignore(e => e.TestDoubleCollectionCollection);
                        b.Ignore(e => e.TestSingleCollectionCollection);
                        b.Ignore(e => e.TestBooleanCollectionCollection);
                        b.Ignore(e => e.TestCharacterCollectionCollection);
                        b.Ignore(e => e.TestDefaultStringCollectionCollection);
                        b.Ignore(e => e.TestMaxLengthStringCollectionCollection);
                        b.Ignore(e => e.TestInt16CollectionCollection);
                        b.Ignore(e => e.TestInt32CollectionCollection);
                        b.Ignore(e => e.TestNullableEnumWithIntConverterCollectionCollection);
                        b.Ignore(e => e.TestNullableInt32CollectionCollection);
                        b.Ignore(e => e.TestNullableEnumCollectionCollection);
                    });
                b.OwnsMany(
                    x => x.Collection, b =>
                    {
                        b.Ignore(e => e.TestInt64CollectionCollection);
                        b.Ignore(e => e.TestDoubleCollectionCollection);
                        b.Ignore(e => e.TestSingleCollectionCollection);
                        b.Ignore(e => e.TestBooleanCollectionCollection);
                        b.Ignore(e => e.TestCharacterCollectionCollection);
                        b.Ignore(e => e.TestDefaultStringCollectionCollection);
                        b.Ignore(e => e.TestMaxLengthStringCollectionCollection);
                        b.Ignore(e => e.TestInt16CollectionCollection);
                        b.Ignore(e => e.TestInt32CollectionCollection);
                        b.Ignore(e => e.TestNullableEnumWithIntConverterCollectionCollection);
                        b.Ignore(e => e.TestNullableInt32CollectionCollection);
                        b.Ignore(e => e.TestNullableEnumCollectionCollection);
                    });
            });
        }
    }
}
