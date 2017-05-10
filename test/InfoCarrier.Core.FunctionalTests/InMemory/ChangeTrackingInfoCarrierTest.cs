namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class ChangeTrackingInfoCarrierTest : ChangeTrackingTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public ChangeTrackingInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
