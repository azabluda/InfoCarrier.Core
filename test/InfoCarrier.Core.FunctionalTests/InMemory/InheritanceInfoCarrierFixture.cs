namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Inheritance;

    public class InheritanceInfoCarrierFixture : InheritanceFixtureBase, IDisposable
    {
        private readonly InfoCarrierTestHelper<InheritanceContext> helper;
        private readonly TestStoreBase testStore;

        public InheritanceInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<InheritanceContext>.CreateHelper(
                this.OnModelCreating,
                opt => new InheritanceContext(opt),
                this.SeedData);

            this.testStore = this.helper.CreateTestStore();
        }

        public override DbContextOptions BuildOptions()
            => this.helper.BuildInfoCarrierOptions(this.testStore.InfoCarrierBackend);

        public void Dispose()
        {
            this.testStore.Dispose();
        }
    }
}
