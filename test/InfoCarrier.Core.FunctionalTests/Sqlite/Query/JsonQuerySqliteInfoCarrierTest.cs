// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestModels.JsonQuery;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
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
    [ConditionalFact]
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
    public override Task Json_predicate_on_byte_array(bool async)
        => Assert.ThrowsAsync<EqualException>(() => base.Json_predicate_on_byte_array(async));

    public override async Task Json_branch_collection_distinct_and_other_collection(bool async)
        => await AssertApplyNotSupported(() => base.Json_branch_collection_distinct_and_other_collection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_Select_entity_in_anonymous_object_ElementAt(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_Select_entity_in_anonymous_object_ElementAt(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_Select_entity_with_initializer_ElementAt(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_Select_entity_with_initializer_ElementAt(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_distinct_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_distinct_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_filter_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_filter_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_in_projection_with_anonymous_projection_of_scalars(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_in_projection_with_anonymous_projection_of_scalars(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_primitive_arrays(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_primitive_arrays(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_scalars(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_scalars(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_leaf_filter_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_leaf_filter_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_of_primitives_SelectMany(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_of_primitives_SelectMany(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_skip_take_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_skip_take_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_skip_take_in_projection_project_into_anonymous_type(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_skip_take_in_projection_project_into_anonymous_type(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_collection_skip_take_in_projection_with_json_reference_access_as_final_operation(bool async)
        => await AssertApplyNotSupported(() => base.Json_collection_skip_take_in_projection_with_json_reference_access_as_final_operation(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_leaf_collection_distinct_and_other_collection(bool async)
        => await AssertApplyNotSupported(() => base.Json_leaf_collection_distinct_and_other_collection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_multiple_collection_projections(bool async)
        => await AssertApplyNotSupported(() => base.Json_multiple_collection_projections(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
    public override async Task Json_nested_collection_anonymous_projection_in_projection(bool async)
        => await AssertApplyNotSupported(() => base.Json_nested_collection_anonymous_projection_in_projection(async));

    /// <inheritdoc cref="Json_branch_collection_distinct_and_other_collection" />
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
    public override Task Json_projection_using_queryable_methods_on_top_of_JSON_collection_AsNoTrackingWithIdentityResolution(
        bool async)
        => Task.CompletedTask;

    /// <inheritdoc cref="Json_projection_using_queryable_methods_on_top_of_JSON_collection_AsNoTrackingWithIdentityResolution" />
    public override Task Json_nested_collection_anonymous_projection_in_projection_NoTrackingWithIdentityResolution(bool async)
        => Task.CompletedTask;

    /// <inheritdoc cref="Json_projection_using_queryable_methods_on_top_of_JSON_collection_AsNoTrackingWithIdentityResolution" />
    public override Task Json_branch_collection_distinct_and_other_collection_AsNoTrackingWithIdentityResolution(bool async)
        => Task.CompletedTask;

    /// <inheritdoc cref="Json_projection_using_queryable_methods_on_top_of_JSON_collection_AsNoTrackingWithIdentityResolution" />
    public override Task Json_collection_SelectMany_AsNoTrackingWithIdentityResolution(bool async)
        => Task.CompletedTask;

    // The three `Project_json_*_tracking_query_fails` overrides that used to sit here are gone:
    // `JsonQueryRelationalTestBase` declares all three, with the same assertion, and R70's
    // re-parent inherits them. They had been mirrored by hand only because the assembly holding
    // that base was not referenced before R1. `OwnsMany_correlated_projection` still raises the
    // same refusal here and EF overrides nothing for it, so that one stays red and stays ours.

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
                InfoCarrierTestStoreFactory.Sqlite,
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
