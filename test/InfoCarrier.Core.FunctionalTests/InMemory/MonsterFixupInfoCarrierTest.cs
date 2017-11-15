// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.TestModels;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit.Abstractions;

    public class MonsterFixupInfoCarrierTest : MonsterFixupTestBase, IDisposable
    {
        private InfoCarrierTestHelper helper;
        private TestStoreBase testStore;

        public MonsterFixupInfoCarrierTest(ITestOutputHelper testOutputHelper)
        {
            // UGLY: determine the current test (http://stackoverflow.com/a/31212346/3253553)
            var testMethod = ((ITest)testOutputHelper
                .GetType()
                .GetField("test", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(testOutputHelper)).TestCase.TestMethod.Method.Name;

            // UGLY: Different tests require different MonsterContext configurations
            // but tests won't explicitly manifest it so we have to guess.
            switch (testMethod)
            {
                case nameof(this.Can_build_monster_model_and_seed_data_using_FKs):
                case nameof(this.Can_build_monster_model_and_seed_data_using_all_navigations):
                case nameof(this.Can_build_monster_model_and_seed_data_using_dependent_navigations):
                case nameof(this.Can_build_monster_model_and_seed_data_using_principal_navigations):
                case nameof(this.Can_build_monster_model_and_seed_data_using_navigations_with_deferred_add):
                case nameof(this.Store_generated_values_are_discarded_if_saving_changes_fails):
                case nameof(this.One_to_many_fixup_happens_when_FKs_change_for_snapshot_entities):
                case nameof(this.One_to_many_fixup_happens_when_reference_changes_for_snapshot_entities):
                case nameof(this.One_to_many_fixup_happens_when_collection_changes_for_snapshot_entities):
                case nameof(this.One_to_one_fixup_happens_when_FKs_change_for_snapshot_entities):
                case nameof(this.One_to_one_fixup_happens_when_reference_change_for_snapshot_entities):
                case nameof(this.Composite_fixup_happens_when_FKs_change_for_snapshot_entities):
                case nameof(this.Fixup_with_binary_keys_happens_when_FKs_or_navigations_change_for_snapshot_entities):
                {
                    this.ConfigureSnapshotMonsterContext();
                    break;
                }

                case nameof(this.Can_build_monster_model_with_full_notification_entities_and_seed_data_using_FKs):
                case nameof(this.Can_build_monster_model_with_full_notification_entities_and_seed_data_using_all_navigations):
                case nameof(this.Can_build_monster_model_with_full_notification_entities_and_seed_data_using_dependent_navigations):
                case nameof(this.Can_build_monster_model_with_full_notification_entities_and_seed_data_using_principal_navigations):
                case nameof(this.Can_build_monster_model_with_full_notification_entities_and_seed_data_using_navigations_with_deferred_add):
                case nameof(this.Store_generated_values_are_discarded_if_saving_changes_fails_with_full_notification_entities):
                case nameof(this.One_to_many_fixup_happens_when_FKs_change_for_full_notification_entities):
                case nameof(this.One_to_many_fixup_happens_when_reference_changes_for_full_notification_entities):
                case nameof(this.One_to_many_fixup_happens_when_collection_changes_for_full_notification_entities):
                case nameof(this.One_to_one_fixup_happens_when_FKs_change_for_full_notification_entities):
                case nameof(this.One_to_one_fixup_happens_when_reference_change_for_full_notification_entities):
                case nameof(this.Composite_fixup_happens_when_FKs_change_for_full_notification_entities):
                case nameof(this.Fixup_with_binary_keys_happens_when_FKs_or_navigations_change_for_full_notification_entities):
                {
                    this.ConfigureChangedChangingMonsterContext();
                    break;
                }

                case nameof(this.Can_build_monster_model_with_changed_only_notification_entities_and_seed_data_using_FKs):
                case nameof(this.Can_build_monster_model_with_changed_only_notification_entities_and_seed_data_using_all_navigations):
                case nameof(this.Can_build_monster_model_with_changed_only_notification_entities_and_seed_data_using_dependent_navigations):
                case nameof(this.Can_build_monster_model_with_changed_only_notification_entities_and_seed_data_using_principal_navigations):
                case nameof(this.Can_build_monster_model_with_changed_only_notification_entities_and_seed_data_using_navigations_with_deferred_add):
                case nameof(this.Store_generated_values_are_discarded_if_saving_changes_fails_with_changed_only_notification_entities):
                case nameof(this.One_to_many_fixup_happens_when_FKs_change_for_changed_only_notification_entities):
                case nameof(this.One_to_many_fixup_happens_when_reference_changes_for_changed_only_notification_entities):
                case nameof(this.One_to_many_fixup_happens_when_collection_changes_for_changed_only_notification_entities):
                case nameof(this.One_to_one_fixup_happens_when_FKs_change_for_changed_only_notification_entities):
                case nameof(this.One_to_one_fixup_happens_when_reference_change_for_changed_only_notification_entities):
                case nameof(this.Composite_fixup_happens_when_FKs_change_for_changed_only_notification_entities):
                case nameof(this.Fixup_with_binary_keys_happens_when_FKs_or_navigations_change_for_changed_only_notification_entities):
                {
                    this.ConfigureChangedOnlyMonsterContext();
                    break;
                }
            }
        }

        private void ConfigureContext<TDbContext>(Func<DbContextOptions, TDbContext> createDbContext)
            where TDbContext : MonsterContext
        {
            this.helper = InMemoryTestStore<TDbContext>.CreateHelper(
                null,
                createDbContext,
                _ => { });

            this.testStore = this.helper.CreateTestStore();
        }

        private void ConfigureSnapshotMonsterContext()
        {
            this.ConfigureContext(opt => new SnapshotMonsterContext(
                opt,
                b =>
                {
                    b.HasChangeTrackingStrategy(ChangeTrackingStrategy.Snapshot);
                    this.OnModelCreating<SnapshotMonsterContext.Message, SnapshotMonsterContext.ProductPhoto, SnapshotMonsterContext.ProductReview>(b);
                }));
        }

        private void ConfigureChangedChangingMonsterContext()
        {
            this.ConfigureContext(opt => new ChangedChangingMonsterContext(
                opt,
                b =>
                {
                    b.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotifications);
                    this.OnModelCreating<ChangedChangingMonsterContext.Message, ChangedChangingMonsterContext.ProductPhoto, ChangedChangingMonsterContext.ProductReview>(b);
                }));
        }

        private void ConfigureChangedOnlyMonsterContext()
        {
            this.ConfigureContext(opt => new ChangedOnlyMonsterContext(
                opt,
                b =>
                {
                    b.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangedNotifications);
                    this.OnModelCreating<ChangedOnlyMonsterContext.Message, ChangedOnlyMonsterContext.ProductPhoto, ChangedOnlyMonsterContext.ProductReview>(b);
                }));
        }

        protected override void CreateAndSeedDatabase(
            string databaseName,
            Func<MonsterContext> createContext,
            Action<MonsterContext> seed)
        {
            using (var context = createContext())
            {
                seed(context);
            }
        }

        protected override DbContextOptions CreateOptions(string databaseName)
        {
            return this.helper.BuildInfoCarrierOptions(this.testStore.InfoCarrierBackend);
        }

        protected override IServiceProvider CreateServiceProvider(bool throwingStateManager = false)
        {
            var serviceCollection = this.helper.ConfigureInfoCarrierServices(new ServiceCollection());

            if (throwingStateManager)
            {
                serviceCollection.AddScoped<IStateManager, ThrowingMonsterStateManager>();
            }

            return serviceCollection.BuildServiceProvider();
        }

        protected override void OnModelCreating<TMessage, TProductPhoto, TProductReview>(ModelBuilder builder)
        {
            base.OnModelCreating<TMessage, TProductPhoto, TProductReview>(builder);

            builder.Entity<TMessage>().Property(e => e.MessageId).ValueGeneratedOnAdd();
            builder.Entity<TProductPhoto>().Property(e => e.PhotoId).ValueGeneratedOnAdd();
            builder.Entity<TProductReview>().Property(e => e.ReviewId).ValueGeneratedOnAdd();
        }

        public void Dispose()
        {
            this.testStore?.Dispose();
        }
    }
}
