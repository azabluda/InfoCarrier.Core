namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
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
