namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class AsyncQueryNavigationsInfoCarrierTest : AsyncQueryNavigationsTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public AsyncQueryNavigationsInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
