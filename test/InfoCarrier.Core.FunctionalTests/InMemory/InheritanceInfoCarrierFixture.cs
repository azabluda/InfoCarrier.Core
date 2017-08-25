namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Inheritance;

    public class InheritanceInfoCarrierFixture : InheritanceFixtureBase<TestStoreBase>
    {
        private readonly InfoCarrierTestHelper<InheritanceContext> helper;

        public InheritanceInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<InheritanceContext>.CreateHelper(
                this.OnModelCreating,
                opt => new InheritanceContext(opt),
                InheritanceModelInitializer.SeedData);
        }

        public override TestStoreBase CreateTestStore()
            => this.helper.CreateTestStore();

        public override InheritanceContext CreateContext(TestStoreBase testStore)
            => this.helper.CreateInfoCarrierContext(testStore);
    }
}
