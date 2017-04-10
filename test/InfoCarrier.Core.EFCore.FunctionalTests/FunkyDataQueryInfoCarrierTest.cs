namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.FunkyDataModel;

    public class FunkyDataQueryInfoCarrierTest
        : FunkyDataQueryTestBase<TestStore, FunkyDataQueryInfoCarrierTest.FunkyDataQueryInfoCarrierFixture>
    {
        public FunkyDataQueryInfoCarrierTest(FunkyDataQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class FunkyDataQueryInfoCarrierFixture : FunkyDataQueryFixtureBase<TestStore>
        {
            private readonly InfoCarrierInMemoryTestHelper<FunkyDataContext> helper;

            public FunkyDataQueryInfoCarrierFixture()
            {
                this.helper = InfoCarrierInMemoryTestHelper.Create(
                    this.OnModelCreating,
                    (opt, _) => new FunkyDataContext(opt));
            }

            public override TestStore CreateTestStore()
                => this.helper.CreateTestStore(FunkyDataModelInitializer.Seed);

            public override FunkyDataContext CreateContext(TestStore testStore)
                => this.helper.CreateInfoCarrierContext();
        }
    }
}