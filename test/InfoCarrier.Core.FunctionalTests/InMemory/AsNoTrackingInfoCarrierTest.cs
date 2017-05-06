namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class AsNoTrackingInfoCarrierTest : AsNoTrackingTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public AsNoTrackingInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
