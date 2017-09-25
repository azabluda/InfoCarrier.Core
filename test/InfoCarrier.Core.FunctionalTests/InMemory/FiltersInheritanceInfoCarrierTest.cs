namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class FiltersInheritanceInfoCarrierTest : FiltersInheritanceTestBase<TestStoreBase, FiltersInheritanceInfoCarrierFixture>
    {
        public FiltersInheritanceInfoCarrierTest(FiltersInheritanceInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
