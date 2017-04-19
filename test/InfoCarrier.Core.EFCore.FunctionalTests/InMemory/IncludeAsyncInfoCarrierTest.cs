namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class IncludeAsyncInfoCarrierTest : IncludeAsyncTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public IncludeAsyncInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
