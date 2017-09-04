namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class DbFunctionsInfoCarrierTest : DbFunctionsTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public DbFunctionsInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
