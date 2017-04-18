namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class NullKeysInfoCarrierTest : NullKeysTestBase<NullKeysInfoCarrierTest.NullKeysInfoCarrierFixture>
    {
        public NullKeysInfoCarrierTest(NullKeysInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class NullKeysInfoCarrierFixture : NullKeysFixtureBase
        {
            private readonly InfoCarrierInMemoryTestHelper<DbContext> helper;

            public NullKeysInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateInMemory(
                    this.OnModelCreating,
                    (opt, _) => new DbContext(opt));

                this.EnsureCreated();
            }

            public override DbContext CreateContext()
                => this.helper.CreateInfoCarrierContext();
        }
    }
}