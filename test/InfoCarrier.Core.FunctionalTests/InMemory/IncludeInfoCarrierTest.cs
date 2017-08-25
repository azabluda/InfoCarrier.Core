namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class IncludeInfoCarrierTest : IncludeTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public IncludeInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
