namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class CompiledQueryInfoCarrierTest : CompiledQueryTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public CompiledQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
