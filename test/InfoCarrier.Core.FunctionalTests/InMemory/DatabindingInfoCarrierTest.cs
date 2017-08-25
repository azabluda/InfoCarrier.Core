namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;

    public class DatabindingInfoCarrierTest : DatabindingTestBase<TestStoreBase, F1InfoCarrierFixture>
    {
        public DatabindingInfoCarrierTest(F1InfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
