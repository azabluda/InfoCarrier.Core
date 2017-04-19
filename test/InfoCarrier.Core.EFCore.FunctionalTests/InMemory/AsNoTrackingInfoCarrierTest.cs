namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
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
