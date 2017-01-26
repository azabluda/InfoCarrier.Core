namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Northwind;
    using Microsoft.Extensions.DependencyInjection;

    public class NorthwindQueryInfoCarrierFixture : NorthwindQueryFixtureBase
    {
        private readonly DbContextOptions options;
        private readonly IInfoCarrierBackend infoCarrierBackend;

        public NorthwindQueryInfoCarrierFixture()
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseInMemoryDatabase();
            var modelBuilder = new ModelBuilder(new CoreConventionSetBuilder().CreateConventionSet());
            this.OnModelCreating(modelBuilder);
            optionsBuilder.UseModel(modelBuilder.Model);
            var backendDbContextOptions = optionsBuilder.Options;
            this.infoCarrierBackend = new TestInfoCarrierBackend(() => new NorthwindContext(backendDbContextOptions));

            this.options = this.BuildOptions();

            using (var context = this.CreateContext())
            {
                NorthwindData.Seed(context);
            }
        }

        public override DbContextOptions BuildOptions(IServiceCollection serviceCollection = null)
            => new DbContextOptionsBuilder()
                .UseInfoCarrierBackend(this.infoCarrierBackend)
                .UseInternalServiceProvider(
                    (serviceCollection ?? new ServiceCollection())
                        .AddEntityFrameworkInfoCarrierBackend()
                        .AddSingleton(TestInfoCarrierModelSource.GetFactory(this.OnModelCreating))
                        .BuildServiceProvider()).Options;

        public override NorthwindContext CreateContext(
            QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => new NorthwindContext(this.options, queryTrackingBehavior);
    }
}