// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.GraphUpdates
{
    using System;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class GraphUpdatesInfoCarrierTest
        : GraphUpdatesTestBase<GraphUpdatesInfoCarrierTest.TestFixture>
    {
        public GraphUpdatesInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public override void Required_many_to_one_dependents_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Optional_many_to_one_dependents_are_orphaned_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_many_to_one_dependents_with_alternate_key_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Optional_many_to_one_dependents_with_alternate_key_are_orphaned_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Optional_one_to_one_relationships_are_one_to_one(
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_one_to_one_relationships_are_one_to_one(
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Save_required_one_to_one_changed_by_reference(
            ChangeMechanism changeMechanism,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Sever_required_one_to_one(
            ChangeMechanism changeMechanism,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_one_to_one_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_non_PK_one_to_one_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Optional_one_to_one_are_orphaned_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_one_to_one_are_cascade_detached_when_Added(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_non_PK_one_to_one_are_cascade_detached_when_Added(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Optional_one_to_one_with_AK_relationships_are_one_to_one(
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_one_to_one_with_AK_relationships_are_one_to_one(
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_one_to_one_with_alternate_key_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_non_PK_one_to_one_with_alternate_key_are_cascade_deleted_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Optional_one_to_one_with_alternate_key_are_orphaned_in_store(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_non_PK_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
        }

        public override void Required_one_to_one_with_alternate_key_are_cascade_detached_when_Added(
            CascadeTiming? cascadeDeleteTiming,
            CascadeTiming? deleteOrphansTiming)
        {
            // FK uniqueness not enforced in in-memory database
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

        public class TestFixture : GraphUpdatesFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override string StoreName { get; } = "InfoCarrierGraphUpdatesTest";

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating,
                    b => b.ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning)));

            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                modelBuilder.UseHiLo(); // ensure model uses sequences

                base.OnModelCreating(modelBuilder, context);
            }
        }
    }
}
