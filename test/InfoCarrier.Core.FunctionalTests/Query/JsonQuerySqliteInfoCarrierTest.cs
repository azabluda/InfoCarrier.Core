// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.JsonQuery;
using Microsoft.EntityFrameworkCore.TestUtilities;

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
