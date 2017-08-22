namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class AsNoTrackingInfoCarrierTest : AsNoTrackingTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public AsNoTrackingInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
