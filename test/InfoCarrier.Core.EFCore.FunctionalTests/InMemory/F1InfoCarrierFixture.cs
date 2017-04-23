namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.ConcurrencyModel;

    public class F1InfoCarrierFixture : F1FixtureBase<TestStoreBase>
    {
        private readonly InfoCarrierTestHelper<F1Context> helper;

        public F1InfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<F1Context>.CreateHelper(
                this.OnModelCreating,
                opt => new F1Context(opt),
                ConcurrencyModelInitializer.Seed);
        }

        public override F1Context CreateContext(TestStoreBase testStore)
            => this.helper.CreateInfoCarrierContext(testStore);

        public override TestStoreBase CreateTestStore()
            => this.helper.CreateTestStore();
    }
}
