namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class NotificationEntitiesInfoCarrierTest
        : NotificationEntitiesTestBase<TestStore, NotificationEntitiesInfoCarrierTest.NotificationEntitiesInfoCarrierFixture>
    {
        public NotificationEntitiesInfoCarrierTest(NotificationEntitiesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class NotificationEntitiesInfoCarrierFixture : NotificationEntitiesFixtureBase
        {
            private readonly InfoCarrierInMemoryTestHelper<DbContext> helper;

            public NotificationEntitiesInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateInMemory(
                    this.OnModelCreating,
                    (opt, _) => new DbContext(opt));
            }

            public override DbContext CreateContext()
                => this.helper.CreateInfoCarrierContext();

            public override TestStore CreateTestStore()
                => this.helper.CreateTestStore(_ => this.EnsureCreated());
        }
    }
}
