namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.ComplexNavigationsModel;

    public class ComplexNavigationsQueryInfoCarrierFixture : ComplexNavigationsQueryFixtureBase<TestStore>
    {
        private readonly InfoCarrierInMemoryTestHelper<ComplexNavigationsContext> helper;

        public ComplexNavigationsQueryInfoCarrierFixture()
        {
            this.helper = InfoCarrierInMemoryTestHelper.Create(
                this.OnModelCreating,
                (opt, _) => new ComplexNavigationsContext(opt));
        }

        public override TestStore CreateTestStore()
            => this.helper.CreateTestStore(ComplexNavigationsModelInitializer.Seed);

        public override ComplexNavigationsContext CreateContext(TestStore testStore)
            => this.helper.CreateInfoCarrierContext();
    }
}
