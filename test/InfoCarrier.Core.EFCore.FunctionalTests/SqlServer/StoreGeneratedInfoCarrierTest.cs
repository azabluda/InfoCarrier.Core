namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class StoreGeneratedInfoCarrierTest
        : StoreGeneratedTestBase<SqlServerTestStore, StoreGeneratedInfoCarrierTest.StoreGeneratedInfoCarrierFixture>
    {
        public StoreGeneratedInfoCarrierTest(StoreGeneratedInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class StoreGeneratedInfoCarrierFixture : StoreGeneratedFixtureBase
        {
            private readonly InfoCarrierSqlServerTestHelper<StoreGeneratedContext> helper;

            public StoreGeneratedInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateSqlServer(
                    "StoreGeneratedTest",
                    this.OnModelCreating,
                    (opt, _) => new StoreGeneratedContext(opt));
            }

            public override SqlServerTestStore CreateTestStore()
                => this.helper.CreateTestStore(_ => { });

            public override DbContext CreateContext(SqlServerTestStore testStore)
                => this.helper.CreateInfoCarrierContext(testStore);

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                modelBuilder.Entity<Gumball>(b =>
                {
                    b.Property(e => e.Id)
                        .UseSqlServerIdentityColumn();

                    b.Property(e => e.Identity)
                        .HasDefaultValue("Banana Joe");

                    b.Property(e => e.IdentityReadOnlyBeforeSave)
                        .HasDefaultValue("Doughnut Sheriff");

                    b.Property(e => e.IdentityReadOnlyAfterSave)
                        .HasDefaultValue("Anton");

                    b.Property(e => e.AlwaysIdentity)
                        .HasDefaultValue("Banana Joe");

                    b.Property(e => e.AlwaysIdentityReadOnlyBeforeSave)
                        .HasDefaultValue("Doughnut Sheriff");

                    b.Property(e => e.AlwaysIdentityReadOnlyAfterSave)
                        .HasDefaultValue("Anton");

                    b.Property(e => e.Computed)
                        .HasDefaultValue("Alan");

                    b.Property(e => e.ComputedReadOnlyBeforeSave)
                        .HasDefaultValue("Carmen");

                    b.Property(e => e.ComputedReadOnlyAfterSave)
                        .HasDefaultValue("Tina Rex");

                    b.Property(e => e.AlwaysComputed)
                        .HasDefaultValue("Alan");

                    b.Property(e => e.AlwaysComputedReadOnlyBeforeSave)
                        .HasDefaultValue("Carmen");

                    b.Property(e => e.AlwaysComputedReadOnlyAfterSave)
                        .HasDefaultValue("Tina Rex");
                });
            }
        }
    }
}