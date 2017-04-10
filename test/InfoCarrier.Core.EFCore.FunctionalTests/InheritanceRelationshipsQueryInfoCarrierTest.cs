namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.InheritanceRelationships;

    public class InheritanceRelationshipsQueryInfoCarrierTest
        : InheritanceRelationshipsQueryTestBase<TestStore, InheritanceRelationshipsQueryInfoCarrierTest.InheritanceRelationshipsQueryInfoCarrierFixture>
    {
        public InheritanceRelationshipsQueryInfoCarrierTest(InheritanceRelationshipsQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class InheritanceRelationshipsQueryInfoCarrierFixture : InheritanceRelationshipsQueryFixtureBase<TestStore>
        {
            private readonly InfoCarrierInMemoryTestHelper<InheritanceRelationshipsContext> helper;

            public InheritanceRelationshipsQueryInfoCarrierFixture()
            {
                this.helper = InfoCarrierInMemoryTestHelper.Create(
                    this.OnModelCreating,
                    (opt, _) => new InheritanceRelationshipsContext(opt));
            }

            public override TestStore CreateTestStore()
                => this.helper.CreateTestStore(InheritanceRelationshipsModelInitializer.Seed);

            public override InheritanceRelationshipsContext CreateContext(TestStore testStore)
                => this.helper.CreateInfoCarrierContext();
        }
    }
}
