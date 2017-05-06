namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class AsTrackingInfoCarrierTest : AsTrackingTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public AsTrackingInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
