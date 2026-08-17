// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
///         <c>TestHelpers.ExecuteWithStrategyInTransactionAsync</c>, so the fixture opts into
///         <see cref="InfoCarrierEventId.TransactionIgnoredWarning" /> and reseeds afterwards —
///         exactly what EF's InMemory base does, and for the same reason: without a real
///         transaction there is no rollback to undo the test's mutations.
///     </para>
/// </remarks>
public class GraphUpdatesInfoCarrierTest(GraphUpdatesInfoCarrierTest.InfoCarrierFixture fixture)
    : GraphUpdatesTestBase<GraphUpdatesInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    // In-memory database does not have database default values
    public override Task Can_insert_when_bool_PK_in_composite_key_has_sentinel_value(bool async, bool initialValue)
        => Task.CompletedTask;

    // In-memory database does not have database default values
    public override Task Can_insert_when_int_PK_in_composite_key_has_sentinel_value(bool async, int initialValue)
        => Task.CompletedTask;

    // In-memory database does not have database default values
    public override Task Can_insert_when_nullable_bool_PK_in_composite_key_has_sentinel_value(bool async, bool? initialValue)
        => Task.CompletedTask;

    // In-memory database does not have database default values
    public override Task Throws_for_single_property_bool_key_with_default_value_generation(bool async, bool initialValue)
        => Task.CompletedTask;

    // In-memory database does not have database default values
    public override Task Throws_for_single_property_nullable_bool_key_with_default_value_generation(bool async, bool? initialValue)
        => Task.CompletedTask;

    // In-memory database does not have database default values
    public override Task Can_insert_when_composite_FK_has_default_value_for_one_part(bool async)
        => Task.CompletedTask;

    // In-memory database does not have database default values
    public override Task Can_insert_when_FK_has_default_value(bool async)
        => Task.CompletedTask;

    // In-memory database does not have database default values
    public override Task Can_insert_when_FK_has_sentinel_value(bool async)
        => Task.CompletedTask;

    public override Task Required_many_to_one_dependents_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Optional_many_to_one_dependents_are_orphaned_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_many_to_one_dependents_with_alternate_key_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Optional_many_to_one_dependents_with_alternate_key_are_orphaned_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Optional_one_to_one_relationships_are_one_to_one(
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_one_to_one_relationships_are_one_to_one(
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Save_required_one_to_one_changed_by_reference(
            ChangeMechanism changeMechanism,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Sever_required_one_to_one(
            ChangeMechanism changeMechanism,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_one_to_one_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_non_PK_one_to_one_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Optional_one_to_one_are_orphaned_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_one_to_one_are_cascade_detached_when_Added(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_non_PK_one_to_one_are_cascade_detached_when_Added(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Optional_one_to_one_with_AK_relationships_are_one_to_one(
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_one_to_one_with_AK_relationships_are_one_to_one(
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_one_to_one_with_alternate_key_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_non_PK_one_to_one_with_alternate_key_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Optional_one_to_one_with_alternate_key_are_orphaned_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_non_PK_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

    public override Task Required_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        // FK uniqueness not enforced in in-memory database
        => Task.CompletedTask;

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
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(w => w.Log(InfoCarrierEventId.TransactionIgnoredWarning));

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
            InfoCarrierBackendTestStore backend = ((InfoCarrierTestStore)TestStore).Backend;
            using DbContext context = backend.CreateDbContext();
            await backend.CleanAsync(context);
            await CleanAsync(context);
            await SeedAsync((PoolableDbContext)context);
        }
    }
}
