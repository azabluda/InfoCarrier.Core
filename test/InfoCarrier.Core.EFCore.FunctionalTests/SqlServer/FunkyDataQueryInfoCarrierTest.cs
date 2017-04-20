namespace InfoCarrier.Core.EFCore.FunctionalTests.SqlServer
{
    using System;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.FunkyDataModel;

    public class FunkyDataQueryInfoCarrierTest
        : FunkyDataQueryTestBase<SqlServerTestStore, FunkyDataQueryInfoCarrierTest.FunkyDataQueryInfoCarrierFixture>
    {
        public FunkyDataQueryInfoCarrierTest(FunkyDataQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class FunkyDataQueryInfoCarrierFixture : FunkyDataQueryFixtureBase<SqlServerTestStore>,
            IDisposable
        {
            private readonly InfoCarrierSqlServerTestHelper<FunkyDataContext> helper;

            public FunkyDataQueryInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateSqlServer(
                    "FunkyDataQueryTest",
                    this.OnModelCreating,
                    (opt, _) => new FunkyDataContext(opt));
            }

            public override SqlServerTestStore CreateTestStore()
                => this.helper.CreateTestStore(FunkyDataModelInitializer.Seed);

            public override FunkyDataContext CreateContext(SqlServerTestStore testStore)
                => this.helper.CreateInfoCarrierContext(testStore);

            public void Dispose()
            {
                this.helper.Dispose();
            }
        }
    }
}