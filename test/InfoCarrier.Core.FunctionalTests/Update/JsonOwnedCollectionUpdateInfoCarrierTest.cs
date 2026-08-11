// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestModels.JsonQuery;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Update;

/// <summary>
///     Writing an <em>owned</em> collection mapped to JSON, on ADR-009 Tier B.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this class exists rather than a spec base.</b> C80 gave the client model the
///         synthesized-ordinal key a JSON-mapped owned collection has on the store, which made 36
///         <c>JsonQuery</c> tests right — and every one of them is a <em>read</em>, because
///         <c>JsonQueryTestBase</c> contains no <c>SaveChanges</c> at all. EF's write coverage for
///         this shape is <c>JsonUpdateTestBase</c>, which C81 measured at <b>142 of 142
///         failing</b>: its <c>UseTransaction</c> is <c>public void</c> rather than virtual and
///         calls the relational <c>GetDbTransaction()</c>, so a provider whose client is not
///         relational cannot reach any of it (ADR-013).
///     </para>
///     <para>
///         Its <b>fixture</b> is reachable, and that is what this uses: EF's own model, EF's own
///         seed, EF's own <c>ToJson()</c> configuration. Only the test bodies are ours, so the
///         transaction is ours to enlist.
///     </para>
///     <para>
///         Each test verifies through a <b>second, fresh client context</b> enlisted in the same
///         transaction, never through the one that wrote. So every assertion is answered by a query
///         that crossed the wire and read SQL — a client that wrote nothing and re-read its own
///         change tracker could not satisfy it. Reading the backing store on a connection of its
///         own cannot work here and was tried first: the write is still inside an uncommitted
///         transaction, and a second connection to a SQLite file does not see it.
///     </para>
/// </remarks>
public class JsonOwnedCollectionUpdateInfoCarrierTest(JsonOwnedCollectionUpdateInfoCarrierTest.InfoCarrierFixture fixture)
    : IClassFixture<JsonOwnedCollectionUpdateInfoCarrierTest.InfoCarrierFixture>
{
    private InfoCarrierFixture Fixture { get; } = fixture;

    [ConditionalFact]
    public Task An_element_added_to_an_owned_JSON_collection_reaches_the_store()
        => TestHelpers.ExecuteWithStrategyInTransactionAsync(
            CreateContext,
            UseTransaction,
            async context =>
            {
                JsonEntityBasic entity = await context.JsonEntitiesBasic.SingleAsync(e => e.Id == 1);
                entity.OwnedCollectionRoot.Add(
                    new JsonOwnedRoot
                    {
                        Name = "added",
                        Number = 99,
                        OwnedCollectionBranch = [],
                    });

                await context.SaveChangesAsync();
            },
            async context =>
            {
                JsonEntityBasic reread = await context.JsonEntitiesBasic.SingleAsync(e => e.Id == 1);
                Assert.Contains(reread.OwnedCollectionRoot, r => r.Name == "added" && r.Number == 99);

                Assert.Contains(reread.OwnedCollectionRoot, r => r.Name == "added" && r.Number == 99);
            });

    [ConditionalFact]
    public Task An_element_edited_in_an_owned_JSON_collection_reaches_the_store()
        => TestHelpers.ExecuteWithStrategyInTransactionAsync(
            CreateContext,
            UseTransaction,
            async context =>
            {
                JsonEntityBasic entity = await context.JsonEntitiesBasic.SingleAsync(e => e.Id == 1);
                entity.OwnedCollectionRoot[1].Name = "edited";

                await context.SaveChangesAsync();
            },
            async context =>
            {
                JsonEntityBasic onDisk = await context.JsonEntitiesBasic.SingleAsync(e => e.Id == 1);

                Assert.Equal("edited", onDisk.OwnedCollectionRoot[1].Name);

                // The element that was *not* edited is still itself. The ordinal key is what keeps
                // an edit on one element instead of on all of them (B12, C80).
                Assert.NotEqual("edited", onDisk.OwnedCollectionRoot[0].Name);
            });

    [ConditionalFact]
    public Task An_element_removed_from_an_owned_JSON_collection_reaches_the_store()
        => TestHelpers.ExecuteWithStrategyInTransactionAsync(
            CreateContext,
            UseTransaction,
            async context =>
            {
                JsonEntityBasic entity = await context.JsonEntitiesBasic.SingleAsync(e => e.Id == 1);
                int before = entity.OwnedCollectionRoot.Count;
                entity.OwnedCollectionRoot.RemoveAt(0);

                await context.SaveChangesAsync();

                Assert.Equal(before - 1, entity.OwnedCollectionRoot.Count);
            },
            async context =>
            {
                JsonEntityBasic onDisk = await context.JsonEntitiesBasic.SingleAsync(e => e.Id == 1);

                Assert.Single(onDisk.OwnedCollectionRoot);
            });

    [ConditionalFact]
    public Task A_new_entity_carrying_an_owned_JSON_collection_reaches_the_store()
        => TestHelpers.ExecuteWithStrategyInTransactionAsync(
            CreateContext,
            UseTransaction,
            async context =>
            {
                context.JsonEntitiesBasic.Add(
                    new JsonEntityBasic
                    {
                        Id = 4242,
                        Name = "new owner",
                        OwnedCollectionRoot =
                        [
                            new JsonOwnedRoot { Name = "first", Number = 1, OwnedCollectionBranch = [] },
                            new JsonOwnedRoot { Name = "second", Number = 2, OwnedCollectionBranch = [] },
                        ],
                    });

                await context.SaveChangesAsync();
            },
            async context =>
            {
                JsonEntityBasic onDisk = await context.JsonEntitiesBasic.SingleAsync(e => e.Id == 4242);

                Assert.Equal(["first", "second"], onDisk.OwnedCollectionRoot.Select(r => r.Name));
            });

    /// <summary>
    ///     B12's own shape, on the write path: two owners, each with its own elements, written in
    ///     one <c>SaveChanges</c>.
    /// </summary>
    /// <remarks>
    ///     Before C80 every element of every owner shared one client key, so this is the assertion
    ///     that would have failed loudest — and it exercises the ordinal key while <em>writing</em>,
    ///     which is the half <c>JsonQueryTestBase</c> cannot reach.
    /// </remarks>
    [ConditionalFact]
    public Task Two_owners_keep_their_own_elements_across_a_write()
        => TestHelpers.ExecuteWithStrategyInTransactionAsync(
            CreateContext,
            UseTransaction,
            async context =>
            {
                JsonEntityBasic first = await context.JsonEntitiesBasic.SingleAsync(e => e.Id == 1);
                first.OwnedCollectionRoot[0].Name = "a-one";

                context.JsonEntitiesBasic.Add(
                    new JsonEntityBasic
                    {
                        Id = 4243,
                        Name = "second owner",
                        OwnedCollectionRoot =
                        [
                            new JsonOwnedRoot { Name = "b-one", Number = 11, OwnedCollectionBranch = [] },
                        ],
                    });

                await context.SaveChangesAsync();
            },
            async context =>
            {
                List<JsonEntityBasic> onDisk = await context.JsonEntitiesBasic.OrderBy(e => e.Id).ToListAsync();

                Assert.Equal(2, onDisk.Count);

                // Each owner keeps exactly its own, which is the whole of B12 stated on the write
                // side: the first gains none of the second's, and the second gains none of the
                // first's.
                Assert.Equal("a-one", onDisk[0].OwnedCollectionRoot[0].Name);
                Assert.Equal(2, onDisk[0].OwnedCollectionRoot.Count);
                Assert.Equal(["b-one"], onDisk[1].OwnedCollectionRoot.Select(r => r.Name));
            });

    private JsonQueryContext CreateContext()
        => Fixture.CreateContext();

    private static void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    public class InfoCarrierFixture : JsonUpdateFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <summary>
        ///     A store of this class's own: these tests write, and the query fixture's store is
        ///     shared with tests that read.
        /// </summary>
        protected override string StoreName
            => "JsonOwnedCollectionUpdateInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     <c>JsonUpdateSqliteFixture</c>'s ignores: SQLite maps no collection of collections.
        ///     The backing store is SQLite, so its limits are ours.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<JsonEntityAllTypes>(b =>
            {
                IgnoreCollectionsOfCollections(b);
                b.OwnsOne(e => e.Reference, IgnoreCollectionsOfCollections);
                b.OwnsMany(e => e.Collection, IgnoreCollectionsOfCollections);
            });
        }

        private static void IgnoreCollectionsOfCollections(EntityTypeBuilder<JsonEntityAllTypes> builder)
        {
            foreach (string name in CollectionsOfCollections)
            {
                builder.Ignore(name);
            }
        }

        private static void IgnoreCollectionsOfCollections<TOwner, TDependent>(
            OwnedNavigationBuilder<TOwner, TDependent> builder)
            where TOwner : class
            where TDependent : class
        {
            foreach (string name in CollectionsOfCollections)
            {
                builder.Ignore(name);
            }
        }

        private static readonly string[] CollectionsOfCollections =
        [
            "TestInt64CollectionCollection",
            "TestDoubleCollectionCollection",
            "TestSingleCollectionCollection",
            "TestBooleanCollectionCollection",
            "TestCharacterCollectionCollection",
            "TestDefaultStringCollectionCollection",
            "TestMaxLengthStringCollectionCollection",
            "TestInt16CollectionCollection",
            "TestInt32CollectionCollection",
            "TestNullableEnumWithIntConverterCollectionCollection",
            "TestNullableInt32CollectionCollection",
            "TestNullableEnumCollectionCollection",
        ];
    }
}
