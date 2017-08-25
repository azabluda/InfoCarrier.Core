namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class ChangeTrackingInfoCarrierTest : ChangeTrackingTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public ChangeTrackingInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
