namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.InheritanceRelationships;

    public class InheritanceRelationshipsQueryInfoCarrierTest
        : InheritanceRelationshipsQueryTestBase<TestStoreBase, InheritanceRelationshipsQueryInfoCarrierTest.InheritanceRelationshipsQueryInfoCarrierFixture>
    {
        public InheritanceRelationshipsQueryInfoCarrierTest(InheritanceRelationshipsQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class InheritanceRelationshipsQueryInfoCarrierFixture : InheritanceRelationshipsQueryFixtureBase<TestStoreBase>
        {
            private readonly InfoCarrierTestHelper<InheritanceRelationshipsContext> helper;

            public InheritanceRelationshipsQueryInfoCarrierFixture()
            {
                this.helper = InMemoryTestStore<InheritanceRelationshipsContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new InheritanceRelationshipsContext(opt),
                    InheritanceRelationshipsModelInitializer.Seed);
            }

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();

            public override InheritanceRelationshipsContext CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);
        }
    }
}
