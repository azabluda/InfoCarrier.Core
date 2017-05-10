namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class LoadInfoCarrierTest
        : LoadTestBase<TestStoreBase, LoadInfoCarrierTest.LoadInfoCarrierFixture>
    {
        public LoadInfoCarrierTest(LoadInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class LoadInfoCarrierFixture : LoadFixtureBase
        {
            private readonly InfoCarrierTestHelper<LoadContext> helper;

            public LoadInfoCarrierFixture()
            {
                this.helper = InMemoryTestStore<LoadContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new LoadContext(opt),
                    this.Seed);
            }

            public override DbContext CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();
        }
    }
}
