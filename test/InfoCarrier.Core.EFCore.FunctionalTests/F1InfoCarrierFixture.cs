namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.ConcurrencyModel;

    public class F1InfoCarrierFixture : F1FixtureBase<TestStore>
    {
        private readonly InfoCarrierInMemoryTestHelper<F1Context> helper;

        public F1InfoCarrierFixture()
        {
            this.helper = InfoCarrierTestHelper.CreateInMemory(
                this.OnModelCreating,
                (opt, _) => new F1Context(opt));
        }

        public override F1Context CreateContext(TestStore testStore)
            => this.helper.CreateInfoCarrierContext();

        public override TestStore CreateTestStore()
            => this.helper.CreateTestStore(ConcurrencyModelInitializer.Seed);
    }
}
