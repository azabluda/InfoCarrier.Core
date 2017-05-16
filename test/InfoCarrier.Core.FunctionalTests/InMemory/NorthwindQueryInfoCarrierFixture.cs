namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Northwind;
    using Microsoft.Extensions.DependencyInjection;

    public class NorthwindQueryInfoCarrierFixture : NorthwindQueryFixtureBase, IDisposable
    {
        private readonly InfoCarrierTestHelper<NorthwindContext> helper;
        private readonly TestStoreBase testStore;

        public NorthwindQueryInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<NorthwindContext>.CreateHelper(
                this.OnModelCreating,
                opt => new NorthwindContext(opt),
                NorthwindData.Seed);

            this.testStore = this.helper.CreateTestStore();
        }

        public override DbContextOptions BuildOptions(IServiceCollection additionalServices = null)
            => this.helper.BuildInfoCarrierOptions(this.testStore.InfoCarrierBackend, additionalServices);

        public void Dispose()
        {
            this.testStore.Dispose();
        }
    }
}