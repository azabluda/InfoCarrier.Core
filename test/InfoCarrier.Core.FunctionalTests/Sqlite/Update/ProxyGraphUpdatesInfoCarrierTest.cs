// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Update;

/// <summary>
///     <c>ProxyGraphUpdatesTestBase</c> on ADR-009 Tier B, in all three proxy flavours.
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
///         <b>Tier B, and it was Tier A until J3.</b> Tier A brought <b>thirteen</b> skips with it,
///         structured after EF's <c>ProxyGraphUpdatesInMemoryTest</c>, and every one is a statement
///         about the InMemory store rather than about proxies: foreign-key constraint checking
///         (issue #2166) and cascade delete (issue #3924). <c>ProxyGraphUpdatesSqliteTest</c> skips
///         <b>none</b> of them. Since the corpus is *reparenting, severing and cascading a graph*,
///         cascade delete is not incidental to it — it is most of the subject, and it was untested
///         here.
///     </para>
/// </remarks>
public class ProxyGraphUpdatesInfoCarrierTest
{
    public abstract class ProxyGraphUpdatesInfoCarrierTestBase<TFixture>(TFixture fixture)
        : ProxyGraphUpdatesTestBase<TFixture>(fixture)
        where TFixture : ProxyGraphUpdatesInfoCarrierTestBase<TFixture>.ProxyGraphUpdatesInfoCarrierFixtureBase, new()
    {
        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         The base opens a transaction on one context and hands every <em>other</em>
        ///         context to this hook to enlist in it. Relational suites do that with
        ///         <c>transaction.GetDbTransaction()</c>, which ADR-013 puts out of reach; the
        ///         InfoCarrier equivalent shares the server's W3 token instead, and the result is
        ///         explicitly not owned — ending it detaches this context and leaves the
        ///         transaction to whoever began it.
        ///     </para>
        ///     <para>
        ///         <b>Without this the whole class deadlocks itself.</b> J3's first attempt moved
        ///         the store and not this hook: the inner contexts ran outside the transaction, the
        ///         outer one held SQLite's write lock, and 471 of 653 failures were
        ///         <c>SQLite Error 5: 'database is locked'</c>, each after a 30-second timeout.
        ///         <c>ConferencePlannerInfoCarrierTest</c> and
        ///         <c>OptimisticConcurrencyInfoCarrierTest</c> already carried this hook for the
        ///         same reason, and its comment there names the same symptom.
        ///     </para>
        /// </remarks>
        protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
            => facade.UseInfoCarrierTransaction(transaction);

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
                    InfoCarrierTestStoreFactory.Sqlite,
                    ContextType,
                    (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                    onAddOptions: AddProxyOptions,
                    onAddServices: s => s.AddEntityFrameworkProxies(),
                    configureConventions: ConfigureConventions);

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
