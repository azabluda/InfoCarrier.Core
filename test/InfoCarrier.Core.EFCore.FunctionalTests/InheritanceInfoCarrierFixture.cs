namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Inheritance;
    using Microsoft.Extensions.DependencyInjection;

    public class InheritanceInfoCarrierFixture : InheritanceFixtureBase
    {
        private static readonly string StoreName = nameof(InheritanceInfoCarrierTest);
        private readonly Func<InheritanceContext> inMemoryDbContextFactory;

        public InheritanceInfoCarrierFixture()
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseInMemoryDatabase(StoreName);
            var modelBuilder = new ModelBuilder(new CoreConventionSetBuilder().CreateConventionSet());
            this.OnModelCreating(modelBuilder);
            optionsBuilder.UseModel(modelBuilder.Model);
            this.inMemoryDbContextFactory = () => new InheritanceContext(optionsBuilder.Options);

            using (var context = this.inMemoryDbContextFactory())
            {
                this.SeedData(context);
            }
        }

        public override InheritanceContext CreateContext()
                => new InheritanceContext(new DbContextOptionsBuilder()
                    .UseInfoCarrierBackend(new TestInfoCarrierBackend(this.inMemoryDbContextFactory, true))
                    .UseInternalServiceProvider(
                        new ServiceCollection()
                            .AddEntityFrameworkInfoCarrierBackend()
                            .AddSingleton(TestInfoCarrierModelSource.GetFactory(this.OnModelCreating))
                            .BuildServiceProvider()).Options);
    }
}
