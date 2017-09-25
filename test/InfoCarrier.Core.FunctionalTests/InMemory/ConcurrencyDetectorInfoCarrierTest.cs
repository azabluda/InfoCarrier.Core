namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;

    public class ConcurrencyDetectorInfoCarrierTest : ConcurrencyDetectorTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public ConcurrencyDetectorInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
