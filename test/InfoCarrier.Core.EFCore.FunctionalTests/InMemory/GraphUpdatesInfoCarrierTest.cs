namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestUtilities.Xunit;

    public class GraphUpdatesInfoCarrierTest
        : GraphUpdatesTestBase<TestStoreBase, GraphUpdatesInfoCarrierTest.GraphUpdatesInfoCarrierFixture>
    {
        public GraphUpdatesInfoCarrierTest(GraphUpdatesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [ConditionalFact]
        public override void Optional_One_to_one_relationships_are_one_to_one()
        {
            // FK uniqueness not enforced in in-memory database
        }

        [ConditionalFact]
        public override void Required_One_to_one_relationships_are_one_to_one()
        {
            // FK uniqueness not enforced in in-memory database
        }

        [ConditionalFact]
        public override void Optional_One_to_one_with_AK_relationships_are_one_to_one()
        {
            // FK uniqueness not enforced in in-memory database
        }

        [ConditionalFact]
        public override void Required_One_to_one_with_AK_relationships_are_one_to_one()
        {
            // FK uniqueness not enforced in in-memory database
        }

        [ConditionalTheory]
        public override void Save_required_one_to_one_changed_by_reference_with_alternate_key(ChangeMechanism changeMechanism, bool useExistingEntities)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Save_required_non_PK_one_to_one_changed_by_reference_with_alternate_key(ChangeMechanism changeMechanism, bool useExistingEntities)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Save_required_one_to_one_changed_by_reference(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Save_removed_required_many_to_one_dependents(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Save_required_non_PK_one_to_one_changed_by_reference(ChangeMechanism changeMechanism, bool useExistingEntities)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Sever_required_one_to_one_with_alternate_key(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Sever_required_one_to_one(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Sever_required_non_PK_one_to_one(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Sever_required_non_PK_one_to_one_with_alternate_key(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_many_to_one_dependents_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_one_to_one_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_non_PK_one_to_one_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_many_to_one_dependents_with_alternate_key_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_one_to_one_with_alternate_key_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_non_PK_one_to_one_with_alternate_key_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Optional_many_to_one_dependents_are_orphaned_in_store()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Optional_one_to_one_are_orphaned_in_store()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Optional_many_to_one_dependents_with_alternate_key_are_orphaned_in_store()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Optional_one_to_one_with_alternate_key_are_orphaned_in_store()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_non_PK_one_to_one_with_alternate_key_are_cascade_detached_when_Added()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_one_to_one_are_cascade_detached_when_Added()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_one_to_one_with_alternate_key_are_cascade_detached_when_Added()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_non_PK_one_to_one_are_cascade_detached_when_Added()
        {
            // Cascade nulls not supported by in-memory database
        }

        public class GraphUpdatesInfoCarrierFixture : GraphUpdatesFixtureBase
        {
            private readonly InfoCarrierTestHelper<GraphUpdatesContext> helper;

            public GraphUpdatesInfoCarrierFixture()
            {
                this.helper = InMemoryTestStore<GraphUpdatesContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new GraphUpdatesContext(opt),
                    this.Seed,
                    false,
                    w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            }

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();

            public override DbContext CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);
        }
    }
}
