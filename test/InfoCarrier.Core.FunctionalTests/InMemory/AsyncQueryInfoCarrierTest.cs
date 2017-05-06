namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class AsyncQueryInfoCarrierTest : AsyncQueryTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public AsyncQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
