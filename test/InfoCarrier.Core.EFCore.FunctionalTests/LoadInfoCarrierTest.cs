namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class LoadInfoCarrierTest
        : LoadTestBase<TestStore, LoadInfoCarrierTest.LoadInfoCarrierFixture>
    {
        public LoadInfoCarrierTest(LoadInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class LoadInfoCarrierFixture : LoadFixtureBase
        {
            private readonly InfoCarrierInMemoryTestHelper<LoadContext> helper;

            public LoadInfoCarrierFixture()
            {
                this.helper = InfoCarrierInMemoryTestHelper.Create(
                    this.OnModelCreating,
                    (opt, _) => new LoadContext(opt));
            }

            public override DbContext CreateContext(TestStore testStore)
                => this.helper.CreateInfoCarrierContext();

            public override TestStore CreateTestStore()
                => this.helper.CreateTestStore(this.Seed);
        }
    }
}
