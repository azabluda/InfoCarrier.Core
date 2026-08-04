// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// A skipped override of an inherited `[ConditionalTheory]` supplies no data of its own, and the
// analyzer cannot see that the base still does. EF's own ProxyGraphUpdatesInMemoryTest carries the
// same overrides and suppresses this in its project file.
#pragma warning disable xUnit1003

namespace InfoCarrier.Core.FunctionalTests.Update;

/// <summary>
///     <c>ProxyGraphUpdatesTestBase</c> on ADR-009 Tier A, in all three proxy flavours.
/// </summary>
/// <remarks>
///     <para>
///         The <c>GraphUpdates</c> corpus again — reparenting, severing and cascading a whole
///         object graph — but over entities that are <em>proxies</em>. That is a pointed
///         combination for this provider: a lazy-loading proxy has to be told what is already
///         loaded rather than load it (Phase L), a change-tracking proxy reports its own edits and
///         nothing re-derives what it missed, and both graphs are reassembled from the wire.
///         `LazyLoadProxyInfoCarrierTest` covers the loading half and `GraphUpdatesInfoCarrierTest`
///         the saving half; this is the only place they meet.
///     </para>
///     <para>
///         Structure and skips are EF's own <c>ProxyGraphUpdatesInMemoryTest</c>, one for one:
///         the FK-constraint and cascade-delete skips are InMemory's limits (issues #2166 and
///         #3924), and the backing store is InMemory.
///     </para>
/// </remarks>
public class ProxyGraphUpdatesInfoCarrierTest
{
    public abstract class ProxyGraphUpdatesInfoCarrierTestBase<TFixture>(TFixture fixture)
        : ProxyGraphUpdatesTestBase<TFixture>(fixture)
        where TFixture : ProxyGraphUpdatesInfoCarrierTestBase<TFixture>.ProxyGraphUpdatesInfoCarrierFixtureBase, new()
    {
        [ConditionalFact(Skip = "FK constraint checking. Issue #2166")]
        public override Task Optional_one_to_one_relationships_are_one_to_one()
            => base.Optional_one_to_one_relationships_are_one_to_one();

        [ConditionalFact(Skip = "FK constraint checking. Issue #2166")]
        public override Task Optional_one_to_one_with_AK_relationships_are_one_to_one()
            => base.Optional_one_to_one_with_AK_relationships_are_one_to_one();

        [ConditionalTheory(Skip = "Cascade delete. Issue #3924")]
        public override Task Optional_many_to_one_dependents_with_alternate_key_are_orphaned_in_store(
            CascadeTiming cascadeDeleteTiming,
            CascadeTiming deleteOrphansTiming)
            => base.Optional_many_to_one_dependents_with_alternate_key_are_orphaned_in_store(
                cascadeDeleteTiming, deleteOrphansTiming);

        [ConditionalTheory(Skip = "Cascade delete. Issue #3924")]
        public override Task Optional_many_to_one_dependents_are_orphaned_in_store(
            CascadeTiming cascadeDeleteTiming,
            CascadeTiming deleteOrphansTiming)
            => base.Optional_many_to_one_dependents_are_orphaned_in_store(cascadeDeleteTiming, deleteOrphansTiming);

        [ConditionalTheory(Skip = "Cascade delete. Issue #3924")]
        public override Task Required_one_to_one_are_cascade_detached_when_Added(
            CascadeTiming cascadeDeleteTiming,
            CascadeTiming deleteOrphansTiming)
            => base.Required_one_to_one_are_cascade_detached_when_Added(cascadeDeleteTiming, deleteOrphansTiming);

        [ConditionalFact(Skip = "FK constraint checking. Issue #2166")]
        public override Task Required_one_to_one_relationships_are_one_to_one()
            => base.Required_one_to_one_relationships_are_one_to_one();

        [ConditionalFact(Skip = "FK constraint checking. Issue #2166")]
        public override Task Required_one_to_one_with_AK_relationships_are_one_to_one()
            => base.Required_one_to_one_with_AK_relationships_are_one_to_one();

        [ConditionalTheory(Skip = "Cascade delete. Issue #3924")]
        public override Task Required_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
            CascadeTiming cascadeDeleteTiming,
            CascadeTiming deleteOrphansTiming)
            => base.Required_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
                cascadeDeleteTiming, deleteOrphansTiming);

        [ConditionalTheory(Skip = "Cascade delete. Issue #3924")]
        public override Task Required_one_to_one_with_alternate_key_are_cascade_deleted_in_store(
            CascadeTiming cascadeDeleteTiming,
            CascadeTiming deleteOrphansTiming)
            => base.Required_one_to_one_with_alternate_key_are_cascade_deleted_in_store(
                cascadeDeleteTiming, deleteOrphansTiming);

        [ConditionalTheory(Skip = "Cascade delete. Issue #3924")]
        public override Task Required_many_to_one_dependents_are_cascade_deleted_in_store(
            CascadeTiming cascadeDeleteTiming,
            CascadeTiming deleteOrphansTiming)
            => base.Required_many_to_one_dependents_are_cascade_deleted_in_store(
                cascadeDeleteTiming, deleteOrphansTiming);

        [ConditionalTheory(Skip = "Cascade delete. Issue #3924")]
        public override Task Required_many_to_one_dependents_with_alternate_key_are_cascade_deleted_in_store(
            CascadeTiming cascadeDeleteTiming,
            CascadeTiming deleteOrphansTiming)
            => base.Required_many_to_one_dependents_with_alternate_key_are_cascade_deleted_in_store(
                cascadeDeleteTiming, deleteOrphansTiming);

        [ConditionalTheory(Skip = "Cascade delete. Issue #3924")]
        public override Task Required_non_PK_one_to_one_are_cascade_detached_when_Added(
            CascadeTiming cascadeDeleteTiming,
            CascadeTiming deleteOrphansTiming)
            => base.Required_non_PK_one_to_one_are_cascade_detached_when_Added(cascadeDeleteTiming, deleteOrphansTiming);

        [ConditionalTheory(Skip = "Cascade delete. Issue #3924")]
        public override Task Required_non_PK_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
            CascadeTiming cascadeDeleteTiming,
            CascadeTiming deleteOrphansTiming)
            => base.Required_non_PK_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
                cascadeDeleteTiming, deleteOrphansTiming);

        /// <inheritdoc />
        /// <remarks>
        ///     The InMemory store has no transaction to roll back, so the data goes back by hand —
        ///     EF's own InMemory base does the same, and so does `GraphUpdatesInfoCarrierTest`.
        /// </remarks>
        protected override async Task ExecuteWithStrategyInTransactionAsync(
            Func<DbContext, Task> testOperation,
            Func<DbContext, Task>? nestedTestOperation1 = null,
            Func<DbContext, Task>? nestedTestOperation2 = null,
            Func<DbContext, Task>? nestedTestOperation3 = null)
        {
            await base.ExecuteWithStrategyInTransactionAsync(
                testOperation, nestedTestOperation1, nestedTestOperation2, nestedTestOperation3);

            await Fixture.ReseedAsync();
        }

        public abstract class ProxyGraphUpdatesInfoCarrierFixtureBase : ProxyGraphUpdatesFixtureBase
        {
            private ITestStoreFactory? _testStoreFactory;

            /// <summary>
            ///     Which proxies this flavour uses. Applied to <em>both</em> contexts.
            /// </summary>
            /// <remarks>
            ///     The seed builds its graph out of <c>context.CreateProxy&lt;Root&gt;()</c>, and it
            ///     runs on the <b>server's</b> context: <see cref="InfoCarrierTestStore" />
            ///     deliberately ignores the fixture's context factory, because the backend owns the
            ///     real store. So the server needs proxies enabled too, or the seed dies with
            ///     "Unable to create proxy for 'Root' because proxies are not enabled" before a
            ///     single test runs. This is the first fixture in the suite whose *seed* needs a
            ///     client-side feature.
            /// </remarks>
            protected abstract DbContextOptionsBuilder AddProxyOptions(DbContextOptionsBuilder builder);

            protected override ITestStoreFactory TestStoreFactory
                => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                    InfoCarrierTestStoreFactory.InMemory,
                    ContextType,
                    (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                    onAddOptions: AddProxyOptions,
                    onAddServices: s => s.AddEntityFrameworkProxies());

            public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                => AddProxyOptions(
                    base.AddOptions(builder.ConfigureWarnings(w => w.Ignore(InfoCarrierEventId.TransactionIgnoredWarning))));

            /// <summary>
            ///     Reseeds through the <em>backend</em> context rather than the client one.
            /// </summary>
            /// <remarks>
            ///     The same override `GraphUpdatesInfoCarrierTest` carries, and for the same
            ///     reason: the initial seed runs server-side, so a reseed that went through a
            ///     client context would make every test's setup depend on remoted `SaveChanges` —
            ///     the thing under test — and, here, would not empty the store at all. Without it
            ///     the seed accumulated and `Optional_many_to_one_dependents_are_orphaned_starting_detached`
            ///     opened with `Assert.Equal(2, root.OptionalChildren.Count())` reading **4**.
            /// </remarks>
            public override async Task ReseedAsync()
            {
                InfoCarrierBackendTestStore backend = ((InfoCarrierTestStore)TestStore).Backend;
                using DbContext context = backend.CreateDbContext();
                await backend.CleanAsync(context);
                await CleanAsync(context);
                await SeedAsync(context);
            }

            protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
                => base.AddServices(serviceCollection.AddEntityFrameworkProxies());
        }
    }

    /// <inheritdoc cref="ProxyGraphUpdatesInfoCarrierTestBase{TFixture}" />
    public class LazyLoading(LazyLoading.ProxyGraphUpdatesWithLazyLoadingInfoCarrierFixture fixture)
        : ProxyGraphUpdatesInfoCarrierTestBase<LazyLoading.ProxyGraphUpdatesWithLazyLoadingInfoCarrierFixture>(fixture)
    {
        protected override bool DoesLazyLoading
            => true;

        protected override bool DoesChangeTracking
            => false;

        public class ProxyGraphUpdatesWithLazyLoadingInfoCarrierFixture : ProxyGraphUpdatesInfoCarrierFixtureBase
        {
            protected override string StoreName
                => "ProxyGraphLazyLoadingUpdatesInfoCarrierTest";

            protected override DbContextOptionsBuilder AddProxyOptions(DbContextOptionsBuilder builder)
                => builder.UseLazyLoadingProxies();
        }
    }

    /// <inheritdoc cref="ProxyGraphUpdatesInfoCarrierTestBase{TFixture}" />
    public class ChangeTracking(ChangeTracking.ProxyGraphUpdatesWithChangeTrackingInfoCarrierFixture fixture)
        : ProxyGraphUpdatesInfoCarrierTestBase<ChangeTracking.ProxyGraphUpdatesWithChangeTrackingInfoCarrierFixture>(fixture)
    {
        /// <inheritdoc />
        /// <remarks>Needs lazy loading, which this flavour does not have. EF skips it too.</remarks>
        public override Task Save_two_entity_cycle_with_lazy_loading()
            => Task.CompletedTask;

        protected override bool DoesLazyLoading
            => false;

        protected override bool DoesChangeTracking
            => true;

        public class ProxyGraphUpdatesWithChangeTrackingInfoCarrierFixture : ProxyGraphUpdatesInfoCarrierFixtureBase
        {
            protected override string StoreName
                => "ProxyGraphChangeTrackingUpdatesInfoCarrierTest";

            protected override DbContextOptionsBuilder AddProxyOptions(DbContextOptionsBuilder builder)
                => builder.UseChangeTrackingProxies();
        }
    }

    /// <inheritdoc cref="ProxyGraphUpdatesInfoCarrierTestBase{TFixture}" />
    public class LazyLoadingAndChangeTracking(
        LazyLoadingAndChangeTracking.ProxyGraphUpdatesWithChangeTrackingInfoCarrierFixture fixture)
        : ProxyGraphUpdatesInfoCarrierTestBase<
            LazyLoadingAndChangeTracking.ProxyGraphUpdatesWithChangeTrackingInfoCarrierFixture>(fixture)
    {
        protected override bool DoesLazyLoading
            => true;

        protected override bool DoesChangeTracking
            => true;

        public class ProxyGraphUpdatesWithChangeTrackingInfoCarrierFixture : ProxyGraphUpdatesInfoCarrierFixtureBase
        {
            protected override string StoreName
                => "ProxyGraphLazyLoadingAndChangeTrackingUpdatesInfoCarrierTest";

            protected override DbContextOptionsBuilder AddProxyOptions(DbContextOptionsBuilder builder)
                => builder
                    .UseChangeTrackingProxies()
                    .UseLazyLoadingProxies();
        }
    }
}
