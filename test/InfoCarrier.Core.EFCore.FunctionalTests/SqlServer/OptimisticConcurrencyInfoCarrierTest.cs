namespace InfoCarrier.Core.EFCore.FunctionalTests.SqlServer
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.ConcurrencyModel;

    public class OptimisticConcurrencyInfoCarrierTest
        : OptimisticConcurrencyTestBase<TestStoreBase, OptimisticConcurrencyInfoCarrierTest.OptimisticConcurrencyInfoCarrierFixture>
    {
        public OptimisticConcurrencyInfoCarrierTest(OptimisticConcurrencyInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class OptimisticConcurrencyInfoCarrierFixture : F1RelationalFixture<TestStoreBase>
        {
            private readonly InfoCarrierTestHelper<F1Context> helper;

            public OptimisticConcurrencyInfoCarrierFixture()
            {
                this.helper = SqlServerTestStore<F1Context>.CreateHelper(
                    this.OnModelCreating,
                    opt => new F1Context(opt),
                    ConcurrencyModelInitializer.Seed,
                    true,
                    "OptimisticConcurrencyTest");
            }

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();

            public override F1Context CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                modelBuilder.Entity<Chassis>().Property<byte[]>("Version").IsRowVersion();
                modelBuilder.Entity<Driver>().Property<byte[]>("Version").IsRowVersion();

                modelBuilder.Entity<Team>().Property<byte[]>("Version")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
        }
    }
}
