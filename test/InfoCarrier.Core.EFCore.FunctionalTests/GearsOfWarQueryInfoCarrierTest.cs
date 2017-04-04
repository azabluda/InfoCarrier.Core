namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class GearsOfWarQueryInfoCarrierTest : GearsOfWarQueryTestBase<TestStore, GearsOfWarQueryInfoCarrierFixture>
    {
        public GearsOfWarQueryInfoCarrierTest(GearsOfWarQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
