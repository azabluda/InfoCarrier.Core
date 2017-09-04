namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;

    public class OwnedQueryInfoCarrierFixture : OwnedQueryFixtureBase, IDisposable
    {
        private readonly InfoCarrierTestHelper<DbContext> helper;
        private readonly TestStoreBase testStore;

        public OwnedQueryInfoCarrierFixture()
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
            this.helper.Dispose();
        }
    }
}
