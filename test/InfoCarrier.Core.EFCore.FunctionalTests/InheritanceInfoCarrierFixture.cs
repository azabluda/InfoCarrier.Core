namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Inheritance;

    public class InheritanceInfoCarrierFixture : InheritanceFixtureBase
    {
        private readonly InfoCarrierInMemoryTestHelper<InheritanceContext> helper;

        public InheritanceInfoCarrierFixture()
        {
            this.helper = InfoCarrierTestHelper.CreateInMemory(
                this.OnModelCreating,
                (opt, _) => new InheritanceContext(opt));

            using (var context = this.helper.CreateBackendContext())
            {
                this.SeedData(context);
            }
        }

        public override InheritanceContext CreateContext()
            => this.helper.CreateInfoCarrierContext();
    }
}
