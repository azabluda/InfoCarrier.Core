namespace InfoCarrier.Core.EFCore.FunctionalTests
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
