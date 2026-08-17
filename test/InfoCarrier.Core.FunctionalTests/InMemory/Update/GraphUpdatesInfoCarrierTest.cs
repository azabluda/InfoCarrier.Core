// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.TestUtilities;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests.InMemory.Update;

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

    protected override async Task ExecuteWithStrategyInTransactionAsync(
        Func<DbContext, Task> testOperation,
        Func<DbContext, Task> nestedTestOperation1 = null,
        Func<DbContext, Task> nestedTestOperation2 = null,
        Func<DbContext, Task> nestedTestOperation3 = null)
    {
        // `finally`, because a failing test dirties the store exactly as a passing one does, and
        // reseeding only on success reports the previous test's leftovers against the next one.
        try
        {
            await base.ExecuteWithStrategyInTransactionAsync(
                testOperation, nestedTestOperation1, nestedTestOperation2, nestedTestOperation3);
        }
        finally
        {
            await Fixture.ReseedAsync();
        }
    }

    public class InfoCarrierFixture : GraphUpdatesFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "GraphUpdatesInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
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
