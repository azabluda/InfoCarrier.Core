namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Northwind;
    using Microsoft.Extensions.DependencyInjection;

    public class NorthwindQueryInfoCarrierFixture : NorthwindQueryFixtureBase
    {
        private readonly InfoCarrierInMemoryTestHelper<NorthwindContext> helper;

        public NorthwindQueryInfoCarrierFixture()
        {
            this.helper = InfoCarrierTestHelper.CreateInMemory(
                this.OnModelCreating,
                (opt, queryTrackingBehavior) => new NorthwindContext(opt, queryTrackingBehavior));

            using (var context = this.helper.CreateBackendContext())
            {
                NorthwindData.Seed(context);
            }
        }

        public override DbContextOptions BuildOptions(IServiceCollection additionalServices = null)
            => this.helper.BuildInfoCarrierOptions(null, additionalServices);

        public override NorthwindContext CreateContext(
            QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.helper.CreateInfoCarrierContext(queryTrackingBehavior);
    }
}