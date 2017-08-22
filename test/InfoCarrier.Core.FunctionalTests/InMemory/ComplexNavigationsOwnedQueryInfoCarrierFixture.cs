namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.ComplexNavigationsModel;

    public class ComplexNavigationsOwnedQueryInfoCarrierFixture : ComplexNavigationsOwnedQueryFixtureBase<TestStoreBase>
    {
        private readonly InfoCarrierTestHelper<ComplexNavigationsContext> helper;

        public ComplexNavigationsOwnedQueryInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<ComplexNavigationsContext>.CreateHelper(
                this.OnModelCreating,
                opt => new ComplexNavigationsContext(opt),
                ctx => ComplexNavigationsModelInitializer.Seed(ctx));
        }

        public override TestStoreBase CreateTestStore()
            => this.helper.CreateTestStore();

        public override ComplexNavigationsContext CreateContext(TestStoreBase testStore)
            => this.helper.CreateInfoCarrierContext(testStore);
    }
}
