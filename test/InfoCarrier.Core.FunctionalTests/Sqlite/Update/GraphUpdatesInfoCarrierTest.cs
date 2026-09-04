// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

// A skipped override of an inherited `[ConditionalTheory]` supplies no data of its own, and the
// analyzer cannot see that the base still does. EF's own GraphUpdatesSqliteTestBase carries the same
// six overrides and suppresses this in its project file.
#pragma warning disable xUnit1003

#nullable disable

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Update;

/// <summary>
///     <c>GraphUpdatesTestBase</c> on ADR-009 Tier A — the InMemory backend. 127 tests covering
///     cascade delete, orphan handling, fixup and severing across every relationship shape,
///     which is the coverage SaveChanges has been missing entirely (its only tests were two
///     hand-written smoke tests).
/// </summary>
/// <remarks>
///     <para>
///         The overrides below are EF Core's own <c>GraphUpdatesInMemoryTestBase</c> overrides,
///         mirrored one for one. They assert nothing about this provider: the backend <em>is</em>
///         the InMemory store, so every InMemory limitation is ours too for as long as Tier A is
///         what is running. Re-test each against Tier B and delete it there where it passes
///         (roadmap M3).
///     </para>
///     <para>
///         Every test in the base runs inside
///         <c>TestHelpers.ExecuteWithStrategyInTransactionAsync</c>, and the fixture reseeds
///         afterwards, exactly as EF's InMemory base does and for the same reason: without a real
///         transaction there is no rollback to undo the test's mutations.
///     </para>
/// </remarks>
public class GraphUpdatesInfoCarrierTest(GraphUpdatesInfoCarrierTest.InfoCarrierFixture fixture)
    : GraphUpdatesTestBase<GraphUpdatesInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    // In-memory database does not have database default values
    // In-memory database does not have database default values
    // In-memory database does not have database default values
    // In-memory database does not have database default values
    // In-memory database does not have database default values
    // In-memory database does not have database default values
    // In-memory database does not have database default values
    // In-memory database does not have database default values
    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         The base opens a transaction on one context and hands every <em>other</em> context
    ///         here to enlist in it. Relational suites do that with
    ///         <c>transaction.GetDbTransaction()</c>, which ADR-013 puts out of reach; the
    ///         InfoCarrier equivalent shares the server's W3 token, and the result is non-owning.
    ///     </para>
    ///     <para>
    ///         <b>Omitting this is what made J3's first attempt take two hours of 30-second lock
    ///         timeouts instead of failing fast</b>, so it lands in the same change as the store
    ///         switch rather than after it.
    ///     </para>
    ///     <para>
    ///         It replaces a by-hand reseed after every test, which existed only because Tier A had
    ///         no transaction to roll back — the same workaround `ConferencePlanner` deleted when it
    ///         gained a real one.
    ///     </para>
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    // EF's own `GraphUpdatesSqliteTestBase` skips, one for one: SQLite cannot express the default
    // owned-collection pattern because of its composite key. Mirrored, not invented.
    private const string OwnedCollectionSkip =
        "Default owned collection pattern does not work with SQLite due to composite key.";

    /// <inheritdoc />
    [ConditionalTheory(Skip = OwnedCollectionSkip)]
    public override Task Delete_principal_with_CLR_key_owned_collection(bool async)
        => base.Delete_principal_with_CLR_key_owned_collection(async);

    /// <inheritdoc />
    [ConditionalTheory(Skip = OwnedCollectionSkip)]
    public override Task Delete_principal_with_shadow_key_owned_collection_throws(bool async)
        => base.Delete_principal_with_shadow_key_owned_collection_throws(async);

    /// <inheritdoc />
    [ConditionalTheory(Skip = OwnedCollectionSkip)]
    public override Task Update_principal_with_CLR_key_owned_collection(bool async)
        => base.Update_principal_with_CLR_key_owned_collection(async);

    /// <inheritdoc />
    [ConditionalTheory(Skip = OwnedCollectionSkip)]
    public override Task Update_principal_with_shadow_key_owned_collection_throws(bool async)
        => base.Update_principal_with_shadow_key_owned_collection_throws(async);

    /// <inheritdoc />
    [ConditionalTheory(Skip = OwnedCollectionSkip)]
    public override Task Clearing_CLR_key_owned_collection(bool async, bool useUpdate, bool addNew)
        => base.Clearing_CLR_key_owned_collection(async, useUpdate, addNew);

    /// <inheritdoc />
    [ConditionalTheory(Skip = OwnedCollectionSkip)]
    public override Task Clearing_shadow_key_owned_collection_throws(bool async, bool useUpdate, bool addNew)
        => base.Clearing_shadow_key_owned_collection_throws(async, useUpdate, addNew);

    public class InfoCarrierFixture : GraphUpdatesFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "GraphUpdatesInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <summary>
        ///     <c>GraphUpdatesSqliteTestBase</c>'s model additions, adopted whole (J14).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <b>These entity types exist only in the relational fixture.</b> `Cruiser`,
        ///         `AccessState`, `SomethingOfCategoryA/B` and `CompositeKeyWith&lt;&gt;` are added
        ///         by <c>GraphUpdatesSqliteTestBase.OnModelCreating</c> and nowhere else, because
        ///         they are configured with <c>HasDefaultValue</c> — a relational model-building
        ///         API. On Tier A they were simply absent, which is why the tests that use them were
        ///         silent no-ops; J12b deleted those overrides and the tests then failed with
        ///         <i>"The entity type 'Cruiser' was not found"</i>. **A missing entity type is a
        ///         model fault, never a store limitation**, which is why this group was the one to
        ///         settle first.
        ///     </para>
        ///     <para>
        ///         <b>It belongs on both halves, and that is the point rather than an inconvenience.</b>
        ///         `ChangeEntryMapper` records the hazard exactly: <i>"the sentinel is computed
        ///         twice … `HasDefaultValue(true)` makes a `bool`'s sentinel `true` on the server and
        ///         leaves it `false` here"</i>. Configuring it once, in the shared
        ///         `OnModelCreating` both sides derive from, is what makes the two agree (A49) —
        ///         which is D2's whole argument.
        ///     </para>
        ///     <para>
        ///         The `OwnerRoot` owned-collection keys come with it. EF *also* skips the six tests
        ///         that exercise them, and those skips are mirrored above; the configuration is kept
        ///         anyway so the model matches EF's rather than diverging in a second way.
        ///     </para>
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<OwnerRoot>(b =>
            {
                b.OwnsMany(
                    e => e.OptionalChildren, b =>
                    {
                        b.HasKey("Id");
                        b.OwnsMany(e => e.Children, b => b.HasKey("Id"));
                    });
                b.OwnsMany(
                    e => e.RequiredChildren, b =>
                    {
                        b.HasKey("Id");
                        b.OwnsMany(e => e.Children, b => b.HasKey("Id"));
                    });
            });

            modelBuilder.Entity<AccessState>(b =>
            {
                b.Property(e => e.AccessStateId).ValueGeneratedNever();
                b.HasData(new AccessState { AccessStateId = 1 });
            });

            modelBuilder.Entity<Cruiser>(b =>
            {
                b.Property(e => e.IdUserState).HasDefaultValue(1);
                b.HasOne(e => e.UserState).WithMany(e => e.Users).HasForeignKey(e => e.IdUserState);
            });

            modelBuilder.Entity<AccessStateWithSentinel>(b =>
            {
                b.Property(e => e.AccessStateWithSentinelId).ValueGeneratedNever();
                b.HasData(new AccessStateWithSentinel { AccessStateWithSentinelId = 1 });
            });

            modelBuilder.Entity<CruiserWithSentinel>(b =>
            {
                b.Property(e => e.IdUserState).HasDefaultValue(1).HasSentinel(667);
                b.HasOne(e => e.UserState).WithMany(e => e.Users).HasForeignKey(e => e.IdUserState);
            });

            modelBuilder.Entity<SomethingOfCategoryA>().Property<int>("CategoryId").HasDefaultValue(1);
            modelBuilder.Entity<SomethingOfCategoryB>().Property(e => e.CategoryId).HasDefaultValue(2);

            modelBuilder.Entity<CompositeKeyWith<int>>(b => b.Property(e => e.PrimaryGroup).HasDefaultValue(1).HasSentinel(1));
            modelBuilder.Entity<CompositeKeyWith<bool>>(b => b.Property(e => e.PrimaryGroup).HasDefaultValue(true));
            modelBuilder.Entity<CompositeKeyWith<bool?>>(b => b.Property(e => e.PrimaryGroup).HasDefaultValue(true));
        }

        /// <summary>
        ///     Reseeds through the <em>backend</em> context rather than the client one.
        /// </summary>
        /// <remarks>
        ///     The base implementation seeds through a client context, which would make every
        ///     test's setup depend on remoted SaveChanges — the thing under test. The initial
        ///     seed already runs server-side (<c>InfoCarrierTestStore.InitializeAsync</c> hands
        ///     it to the backend), so this keeps seeding to one mechanism.
        /// </remarks>
        public override async Task ReseedAsync()
        {
            InfoCarrierBackendTestStore backend = ((IInfoCarrierClientTestStore)TestStore).Backend;
            using DbContext context = backend.CreateDbContext();
            await backend.CleanAsync(context);
            await CleanAsync(context);
            await SeedAsync((PoolableDbContext)context);
        }
    }
}
