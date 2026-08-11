// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestModels.JsonQuery;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.FunctionalTests.Query;

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
///         The <em>core</em> base, not <c>JsonQueryRelationalTestBase</c>, which asserts SQL. The
///         <c>ToJson()</c> mapping and the SQLite ignores below are copied from
///         <c>JsonQueryRelationalFixture</c> and <c>JsonQuerySqliteFixture</c> with their reasons:
///         a JSON column is what the base is named for, so leaving it out would adopt the base's
///         name without its subject.
///     </para>
/// </remarks>
public class JsonQuerySqliteInfoCarrierTest(
    JsonQuerySqliteInfoCarrierTest.JsonQuerySqliteInfoCarrierFixture fixture)
    : JsonQueryTestBase<JsonQuerySqliteInfoCarrierTest.JsonQuerySqliteInfoCarrierFixture>(fixture)
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
    ///         was not an exact match"). One of ours goes the other way —
    ///         <c>Json_nested_collection_anonymous_projection_of_primitives_in_projection_NoTrackingWithIdentityResolution</c>
    ///         raises <c>APPLY</c> here and EF does not override it — and is left red for the same
    ///         reason in reverse: it is not EF's limitation, so it is not EF's override to borrow.
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
        using DbContext server = ((InfoCarrierTestStore)Fixture.TestStore).Backend.CreateDbContext();

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
    ///     Three the core base leaves to the provider, mirrored from
    ///     <c>JsonQueryRelationalTestBase</c>.
    /// </summary>
    /// <remarks>
    ///     The base says so in a comment on the test itself — *"verify exception on the provider
    ///     level, relational and core throw different exceptions"* — and then projects an owned
    ///     entity out of a tracking query without its owner, which is a thing EF refuses. The
    ///     relational base asserts the refusal's message; this provider raises the same one, from
    ///     the same place, because the query it raises on is the one the server ran.
    ///     <para>
    ///         Not adopted for the fourth test that fails this way. `OwnsMany_correlated_projection`
    ///         raises it here and EF overrides nothing — it passes there — so that one is a failure
    ///         of ours and stays red.
    ///     </para>
    /// </remarks>
    public override async Task Project_json_reference_in_tracking_query_fails(bool async)
        => await AssertOwnedWithoutOwner(() => base.Project_json_reference_in_tracking_query_fails(async));

    /// <inheritdoc cref="Project_json_reference_in_tracking_query_fails" />
    public override async Task Project_json_collection_in_tracking_query_fails(bool async)
        => await AssertOwnedWithoutOwner(() => base.Project_json_collection_in_tracking_query_fails(async));

    /// <inheritdoc cref="Project_json_reference_in_tracking_query_fails" />
    public override async Task Project_json_entity_in_tracking_query_fails_even_when_owner_is_present(bool async)
        => await AssertOwnedWithoutOwner(
            () => base.Project_json_entity_in_tracking_query_fails_even_when_owner_is_present(async));

    private static async Task AssertOwnedWithoutOwner(Func<Task> query)
        => Assert.Equal(
            CoreStrings.OwnedEntitiesCannotBeTrackedWithoutTheirOwner,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);

    private static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);

    public class JsonQuerySqliteInfoCarrierFixture : JsonQueryFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "JsonQuerySqliteInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     <c>JsonQueryRelationalFixture</c>'s <c>ToJson()</c> calls, then
        ///     <c>JsonQuerySqliteFixture</c>'s ignores — SQLite does not map a collection of
        ///     collections. Both are the backing store's statements about how these owned types
        ///     are stored, which is why they are mirrored rather than invented.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<JsonEntityBasic>().OwnsOne(x => x.OwnedReferenceRoot).ToJson();
            modelBuilder.Entity<JsonEntityBasic>().OwnsMany(x => x.OwnedCollectionRoot).ToJson();

            modelBuilder.Entity<JsonEntityCustomNaming>().OwnsOne(
                x => x.OwnedReferenceRoot, b =>
                {
                    b.ToJson("json_reference_custom_naming");
                    b.OwnsOne(x => x.OwnedReferenceBranch);
                    b.OwnsMany(x => x.OwnedCollectionBranch);
                });

            modelBuilder.Entity<JsonEntityCustomNaming>().OwnsMany(
                x => x.OwnedCollectionRoot, b =>
                {
                    b.ToJson("json_collection_custom_naming");
                    b.OwnsOne(x => x.OwnedReferenceBranch);
                    b.OwnsMany(x => x.OwnedCollectionBranch);
                });

            modelBuilder.Entity<JsonEntitySingleOwned>().OwnsMany(x => x.OwnedCollection).ToJson();

            modelBuilder.Entity<JsonEntityInheritanceBase>(b =>
            {
                b.OwnsOne(x => x.ReferenceOnBase).ToJson();
                b.OwnsMany(x => x.CollectionOnBase).ToJson();
            });

            modelBuilder.Entity<JsonEntityInheritanceDerived>(b =>
            {
                b.HasBaseType<JsonEntityInheritanceBase>();
                b.OwnsOne(x => x.ReferenceOnDerived).ToJson();
                b.OwnsMany(x => x.CollectionOnDerived).ToJson();
            });

            modelBuilder.Entity<JsonEntityAllTypes>().OwnsOne(x => x.Reference).ToJson();
            modelBuilder.Entity<JsonEntityAllTypes>().OwnsMany(x => x.Collection).ToJson();

            modelBuilder.Entity<JsonEntityConverters>().OwnsOne(x => x.Reference).ToJson();

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
