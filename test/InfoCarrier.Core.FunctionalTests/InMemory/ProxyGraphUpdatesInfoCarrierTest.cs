// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;

    public abstract class ProxyGraphUpdatesInfoCarrierTest
    {
        public abstract class ProxyGraphUpdatesInfoCarrierTestBase<TFixture> : ProxyGraphUpdatesTestBase<TFixture>
            where TFixture : ProxyGraphUpdatesTestBase<TFixture>.ProxyGraphUpdatesFixtureBase, new()
        {
            protected ProxyGraphUpdatesInfoCarrierTestBase(TFixture fixture)
                : base(fixture)
            {
            }

            // #11552
            public override void Save_required_one_to_one_changed_by_reference(ChangeMechanism changeMechanism)
            {
            }

            public override void Optional_one_to_one_relationships_are_one_to_one()
            {
            }

            public override void Optional_one_to_one_with_AK_relationships_are_one_to_one()
            {
            }

            public override void Optional_many_to_one_dependents_with_alternate_key_are_orphaned_in_store(
                CascadeTiming cascadeDeleteTiming,
                CascadeTiming deleteOrphansTiming)
            {
            }

            public override void Optional_many_to_one_dependents_are_orphaned_in_store(
                CascadeTiming cascadeDeleteTiming,
                CascadeTiming deleteOrphansTiming)
            {
            }

            public override void Required_one_to_one_are_cascade_detached_when_Added(
                CascadeTiming cascadeDeleteTiming,
                CascadeTiming deleteOrphansTiming)
            {
            }

            public override void Required_one_to_one_relationships_are_one_to_one()
            {
            }

            public override void Required_one_to_one_with_AK_relationships_are_one_to_one()
            {
            }

            public override void Required_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
                CascadeTiming cascadeDeleteTiming,
                CascadeTiming deleteOrphansTiming)
            {
            }

            public override void Required_one_to_one_with_alternate_key_are_cascade_deleted_in_store(
                CascadeTiming cascadeDeleteTiming,
                CascadeTiming deleteOrphansTiming)
            {
            }

            public override void Required_many_to_one_dependents_are_cascade_deleted_in_store(
                CascadeTiming cascadeDeleteTiming,
                CascadeTiming deleteOrphansTiming)
            {
            }

            public override void Required_many_to_one_dependents_with_alternate_key_are_cascade_deleted_in_store(
                CascadeTiming cascadeDeleteTiming,
                CascadeTiming deleteOrphansTiming)
            {
            }

            public override void Required_non_PK_one_to_one_are_cascade_detached_when_Added(
                CascadeTiming cascadeDeleteTiming,
                CascadeTiming deleteOrphansTiming)
            {
            }

            public override void Required_non_PK_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
                CascadeTiming cascadeDeleteTiming,
                CascadeTiming deleteOrphansTiming)
            {
            }

            protected override void ExecuteWithStrategyInTransaction(
                Action<DbContext> testOperation,
                Action<DbContext> nestedTestOperation1 = null,
                Action<DbContext> nestedTestOperation2 = null,
                Action<DbContext> nestedTestOperation3 = null)
            {
                base.ExecuteWithStrategyInTransaction(testOperation, nestedTestOperation1, nestedTestOperation2, nestedTestOperation3);
                this.Fixture.Reseed();
            }
        }

        public class LazyLoading : ProxyGraphUpdatesInfoCarrierTestBase<LazyLoading.TestFixture>
        {
            public LazyLoading(TestFixture fixture)
                : base(fixture)
            {
            }

            protected override bool DoesLazyLoading
                => true;

            protected override bool DoesChangeTracking
                => false;

            public class TestFixture : ProxyGraphUpdatesFixtureBase
            {
                private ITestStoreFactory testStoreFactory;

                protected override ITestStoreFactory TestStoreFactory =>
                    InfoCarrierTestStoreFactory.EnsureInitialized(
                        ref this.testStoreFactory,
                        InfoCarrierTestStoreFactory.InMemory,
                        this.ContextType,
                        this.OnModelCreating,
                        b => b.ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning))
                              .UseLazyLoadingProxies());

                protected override string StoreName { get; } = "ProxyGraphLazyLoadingUpdatesTest";

                public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                    => base.AddOptions(builder.UseLazyLoadingProxies());

                protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
                    => base.AddServices(serviceCollection.AddEntityFrameworkProxies());

                protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
                {
                    modelBuilder.UseIdentityColumns();

                    base.OnModelCreating(modelBuilder, context);
                }
            }
        }

        public class ChangeTracking : ProxyGraphUpdatesInfoCarrierTestBase<ChangeTracking.TestFixture>
        {
            public ChangeTracking(TestFixture fixture)
                : base(fixture)
            {
            }

            protected override bool DoesLazyLoading
                => false;

            protected override bool DoesChangeTracking
                => true;

            public class TestFixture : ProxyGraphUpdatesFixtureBase
            {
                private ITestStoreFactory testStoreFactory;

                protected override ITestStoreFactory TestStoreFactory =>
                    InfoCarrierTestStoreFactory.EnsureInitialized(
                        ref this.testStoreFactory,
                        InfoCarrierTestStoreFactory.InMemory,
                        this.ContextType,
                        this.OnModelCreating,
                        b => b.ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning))
                              .UseChangeTrackingProxies());

                protected override string StoreName { get; } = "ProxyGraphChangeTrackingUpdatesTest";

                public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                    => base.AddOptions(builder.UseChangeTrackingProxies());

                protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
                    => base.AddServices(serviceCollection.AddEntityFrameworkProxies());

                protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
                {
                    modelBuilder.UseIdentityColumns();
                    modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues);

                    base.OnModelCreating(modelBuilder, context);
                }
            }
        }

        public class ChangeTrackingAndLazyLoading : ProxyGraphUpdatesInfoCarrierTestBase<
            ChangeTrackingAndLazyLoading.TestFixture>
        {
            public ChangeTrackingAndLazyLoading(TestFixture fixture)
                : base(fixture)
            {
            }

            protected override bool DoesLazyLoading
                => true;

            protected override bool DoesChangeTracking
                => true;

            public class TestFixture : ProxyGraphUpdatesFixtureBase
            {
                private ITestStoreFactory testStoreFactory;

                protected override ITestStoreFactory TestStoreFactory =>
                    InfoCarrierTestStoreFactory.EnsureInitialized(
                        ref this.testStoreFactory,
                        InfoCarrierTestStoreFactory.InMemory,
                        this.ContextType,
                        this.OnModelCreating,
                        b => b.ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning))
                              .UseLazyLoadingProxies()
                              .UseChangeTrackingProxies());

                protected override string StoreName { get; } = "ProxyGraphChangeTrackingAndLazyLoadingUpdatesTest";

                public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                    => base.AddOptions(builder.UseLazyLoadingProxies().UseChangeTrackingProxies());

                protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
                    => base.AddServices(serviceCollection.AddEntityFrameworkProxies());

                protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
                {
                    modelBuilder.UseIdentityColumns();
                    modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues);

                    base.OnModelCreating(modelBuilder, context);
                }
            }
        }
    }
}
