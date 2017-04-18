namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class FieldMappingInfoCarrierTest
        : FieldMappingTestBase<TestStore, FieldMappingInfoCarrierTest.FieldMappingInfoCarrierFixture>
    {
        public FieldMappingInfoCarrierTest(FieldMappingInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class FieldMappingInfoCarrierFixture : FieldMappingFixtureBase
        {
            private readonly InfoCarrierInMemoryTestHelper<FieldMappingContext> helper;

            public FieldMappingInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateInMemory(
                    this.OnModelCreating,
                    (opt, _) => new FieldMappingContext(opt),
                    w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            }

            public override DbContext CreateContext(TestStore testStore)
                => this.helper.CreateInfoCarrierContext();

            public override TestStore CreateTestStore()
                => this.helper.CreateTestStore(this.Seed);
        }
    }
}
