namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.GearsOfWarModel;

    public class GearsOfWarQueryInfoCarrierFixture : GearsOfWarQueryFixtureBase<TestStore>
    {
        private readonly InfoCarrierInMemoryTestHelper<GearsOfWarContext> helper;

        public GearsOfWarQueryInfoCarrierFixture()
        {
            this.helper = InfoCarrierTestHelper.CreateInMemory(
                this.OnModelCreating,
                (opt, _) => new GearsOfWarContext(opt));
        }

        public override TestStore CreateTestStore()
            => this.helper.CreateTestStore(GearsOfWarModelInitializer.Seed, true);

        public override GearsOfWarContext CreateContext(TestStore testStore)
        {
            var context = this.helper.CreateInfoCarrierContext();

            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            return context;
        }
    }
}
