namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class DatabindingInfoCarrierTest : DatabindingTestBase<TestStore, F1InfoCarrierFixture>
    {
        public DatabindingInfoCarrierTest(F1InfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
