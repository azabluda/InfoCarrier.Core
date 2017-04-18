namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class OneToOneQueryInfoCarrierFixture : OneToOneQueryFixtureBase
    {
        private readonly InfoCarrierInMemoryTestHelper<DbContext> helper;

        public OneToOneQueryInfoCarrierFixture()
        {
            this.helper = InfoCarrierTestHelper.CreateInMemory(
                this.OnModelCreating,
                (opt, _) => new DbContext(opt));

            using (var context = this.helper.CreateBackendContext())
            {
                AddTestData(context);
            }
        }

        public DbContext CreateContext()
            => this.helper.CreateInfoCarrierContext();
    }
}
