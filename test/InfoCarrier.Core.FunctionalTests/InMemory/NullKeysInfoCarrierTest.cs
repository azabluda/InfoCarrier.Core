namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class NullKeysInfoCarrierTest : NullKeysTestBase<NullKeysInfoCarrierTest.NullKeysInfoCarrierFixture>
    {
        public NullKeysInfoCarrierTest(NullKeysInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class NullKeysInfoCarrierFixture : NullKeysFixtureBase, IDisposable
        {
            private readonly InfoCarrierTestHelper<DbContext> helper;
            private readonly TestStoreBase testStore;

            public NullKeysInfoCarrierFixture()
            {
                this.helper = InMemoryTestStore<DbContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new DbContext(opt),
                    _ => this.EnsureCreated());

                this.testStore = this.helper.CreateTestStore();
            }

            public override DbContext CreateContext()
                => this.helper.CreateInfoCarrierContext(this.testStore);

            public void Dispose()
            {
                this.testStore.Dispose();
            }
        }
    }
}