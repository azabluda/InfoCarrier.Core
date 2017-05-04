namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class IncludeInfoCarrierTest : IncludeTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public IncludeInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
