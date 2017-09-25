namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Northwind;
    using Microsoft.Extensions.DependencyInjection;

    public class NorthwindQueryInfoCarrierFixture : NorthwindQueryFixtureBase, IDisposable
    {
        private readonly InfoCarrierTestHelper<NorthwindContext> helper;
        private readonly TestStoreBase testStore;

        public NorthwindQueryInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<NorthwindContext>.CreateHelper(
                this.OnModelCreating,
                (opt, clientDbContext) =>
                {
                    var dbContext = new NorthwindContext(opt);
                    if (clientDbContext != null)
                    {
                        dbContext.TenantPrefix = clientDbContext.TenantPrefix;
                    }
                    return dbContext;
                },
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