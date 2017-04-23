namespace InfoCarrier.Core.EFCore.FunctionalTests.SqlServer
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.FunkyDataModel;

    public class FunkyDataQueryInfoCarrierTest
        : FunkyDataQueryTestBase<TestStoreBase, FunkyDataQueryInfoCarrierTest.FunkyDataQueryInfoCarrierFixture>
    {
        public FunkyDataQueryInfoCarrierTest(FunkyDataQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class FunkyDataQueryInfoCarrierFixture : FunkyDataQueryFixtureBase<TestStoreBase>
        {
            private readonly InfoCarrierTestHelper<FunkyDataContext> helper;

            public FunkyDataQueryInfoCarrierFixture()
            {
                this.helper = SqlServerTestStore<FunkyDataContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new FunkyDataContext(opt),
                    FunkyDataModelInitializer.Seed,
                    true,
                    "FunkyDataQueryTest");
            }

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();

            public override FunkyDataContext CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);
        }
    }
}