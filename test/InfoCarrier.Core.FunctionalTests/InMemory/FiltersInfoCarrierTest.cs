namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class FiltersInfoCarrierTest : FiltersTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public FiltersInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
