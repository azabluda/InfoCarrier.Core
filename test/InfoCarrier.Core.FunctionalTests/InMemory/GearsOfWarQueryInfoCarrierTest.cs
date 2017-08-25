namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class GearsOfWarQueryInfoCarrierTest : GearsOfWarQueryTestBase<TestStoreBase, GearsOfWarQueryInfoCarrierFixture>
    {
        public GearsOfWarQueryInfoCarrierTest(GearsOfWarQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
