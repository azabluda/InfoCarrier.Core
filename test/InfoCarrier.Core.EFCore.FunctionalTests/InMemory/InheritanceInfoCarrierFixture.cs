namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using System;
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

        public override InheritanceContext CreateContext()
            => this.helper.CreateInfoCarrierContext(this.testStore);

        public void Dispose()
        {
            this.testStore.Dispose();
        }
    }
}
