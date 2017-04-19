namespace InfoCarrier.Core.EFCore.FunctionalTests.SqlServer
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.ConcurrencyModel;

    public class OptimisticConcurrencyInfoCarrierTest
        : OptimisticConcurrencyTestBase<TestStore, OptimisticConcurrencyInfoCarrierTest.OptimisticConcurrencyInfoCarrierFixture>
    {
        public OptimisticConcurrencyInfoCarrierTest(OptimisticConcurrencyInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class OptimisticConcurrencyInfoCarrierFixture : F1FixtureBase<TestStore>
        {
            private readonly InfoCarrierInMemoryTestHelper<F1Context> helper;

            public OptimisticConcurrencyInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateInMemory(
                    this.OnModelCreating,
                    (opt, _) => new F1Context(opt),
                    w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            }

            public override TestStore CreateTestStore()
                => this.helper.CreateTestStore(ConcurrencyModelInitializer.Seed);

            public override F1Context CreateContext(TestStore testStore)
                => this.helper.CreateInfoCarrierContext();
        }
    }
}
