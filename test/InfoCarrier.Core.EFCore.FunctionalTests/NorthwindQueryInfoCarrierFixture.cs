namespace InfoCarrier.Core.EFCore.FunctionalTests
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
            this.helper = InfoCarrierInMemoryTestHelper.Create(
                this.OnModelCreating,
                (opt, queryTrackingBehavior) => new NorthwindContext(opt, queryTrackingBehavior));

            using (var context = this.helper.CreateInMemoryContext())
            {
                NorthwindData.Seed(context);
            }
        }

        public override DbContextOptions BuildOptions(IServiceCollection additionalServices = null)
            => this.helper.BuildInfoCarrierOptions(additionalServices);

        public override NorthwindContext CreateContext(
            QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.helper.CreateInfoCarrierContext(queryTrackingBehavior);
    }
}