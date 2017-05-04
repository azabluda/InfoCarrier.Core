namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class OneToOneQueryInfoCarrierFixture : OneToOneQueryFixtureBase, IDisposable
    {
        private readonly InfoCarrierTestHelper<DbContext> helper;
        private readonly TestStoreBase testStore;

        public OneToOneQueryInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<DbContext>.CreateHelper(
                this.OnModelCreating,
                opt => new DbContext(opt),
                AddTestData);

            this.testStore = this.helper.CreateTestStore();
        }

        public DbContext CreateContext()
            => this.helper.CreateInfoCarrierContext(this.testStore);

        public void Dispose()
        {
            this.testStore.Dispose();
        }
    }
}
