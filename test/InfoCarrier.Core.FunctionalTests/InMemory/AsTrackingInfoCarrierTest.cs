namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class AsTrackingInfoCarrierTest : AsTrackingTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public AsTrackingInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
