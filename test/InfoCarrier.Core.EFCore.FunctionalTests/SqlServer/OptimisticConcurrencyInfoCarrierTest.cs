namespace InfoCarrier.Core.EFCore.FunctionalTests.SqlServer
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.ConcurrencyModel;

    public class OptimisticConcurrencyInfoCarrierTest
        : OptimisticConcurrencyTestBase<SqlServerTestStore, OptimisticConcurrencyInfoCarrierTest.OptimisticConcurrencyInfoCarrierFixture>
    {
        public OptimisticConcurrencyInfoCarrierTest(OptimisticConcurrencyInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class OptimisticConcurrencyInfoCarrierFixture : F1RelationalFixture<SqlServerTestStore>
        {
            private readonly InfoCarrierSqlServerTestHelper<F1Context> helper;

            public OptimisticConcurrencyInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateSqlServer(
                    "OptimisticConcurrencyTest",
                    this.OnModelCreating,
                    (opt, _) => new F1Context(opt));
            }

            public override SqlServerTestStore CreateTestStore()
                => this.helper.CreateTestStore(ConcurrencyModelInitializer.Seed);

            public override F1Context CreateContext(SqlServerTestStore testStore)
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
