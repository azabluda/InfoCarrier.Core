namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;

    public abstract class FindInfoCarrierTest
        : FindTestBase<TestStoreBase, FindInfoCarrierTest.FindInfoCarrierFixture>
    {
        protected FindInfoCarrierTest(FindInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class FindInfoCarrierTestSet : FindInfoCarrierTest
        {
            public FindInfoCarrierTestSet(FindInfoCarrierFixture fixture)
                : base(fixture)
            {
            }

            protected override TEntity Find<TEntity>(DbContext context, params object[] keyValues)
                => context.Set<TEntity>().Find(keyValues);

            protected override Task<TEntity> FindAsync<TEntity>(DbContext context, params object[] keyValues)
                => context.Set<TEntity>().FindAsync(keyValues);
        }

        public class FindInfoCarrierTestContext : FindInfoCarrierTest
        {
            public FindInfoCarrierTestContext(FindInfoCarrierFixture fixture)
                : base(fixture)
            {
            }

            protected override TEntity Find<TEntity>(DbContext context, params object[] keyValues)
                => context.Find<TEntity>(keyValues);

            protected override Task<TEntity> FindAsync<TEntity>(DbContext context, params object[] keyValues)
                => context.FindAsync<TEntity>(keyValues);
        }

        public class FindInfoCarrierTestNonGeneric : FindInfoCarrierTest
        {
            public FindInfoCarrierTestNonGeneric(FindInfoCarrierFixture fixture)
                : base(fixture)
            {
            }

            protected override TEntity Find<TEntity>(DbContext context, params object[] keyValues)
                => (TEntity)context.Find(typeof(TEntity), keyValues);

            protected override async Task<TEntity> FindAsync<TEntity>(DbContext context, params object[] keyValues)
                => (TEntity)await context.FindAsync(typeof(TEntity), keyValues);
        }

        public class FindInfoCarrierFixture : FindFixtureBase
        {
            private readonly InfoCarrierTestHelper<FindContext> helper;

            public FindInfoCarrierFixture()
            {
                this.helper = InMemoryTestStore<FindContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new FindContext(opt),
                    this.Seed);
            }

            public override DbContext CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();
        }
    }
}
