namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class StoreGeneratedFixupInfoCarrierTest
        : StoreGeneratedFixupTestBase<TestStore, StoreGeneratedFixupInfoCarrierTest.StoreGeneratedFixupInfoCarrierFixture>
    {
        public StoreGeneratedFixupInfoCarrierTest(StoreGeneratedFixupInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        protected override bool EnforcesFKs => false;

        public class StoreGeneratedFixupInfoCarrierFixture : StoreGeneratedFixupFixtureBase
        {
            private readonly InfoCarrierInMemoryTestHelper<StoreGeneratedFixupContext> helper;

            public StoreGeneratedFixupInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateInMemory(
                    this.OnModelCreating,
                    (opt, _) => new StoreGeneratedFixupContext(opt),
                    w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            }

            public override TestStore CreateTestStore()
                => this.helper.CreateTestStore(this.Seed);

            public override DbContext CreateContext(TestStore testStore)
                => this.helper.CreateInfoCarrierContext();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                modelBuilder.Entity<Parent>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<Child>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<ParentPN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<ChildPN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<ParentDN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<ChildDN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<ParentNN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<ChildNN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<CategoryDN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<ProductDN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<CategoryPN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<ProductPN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<CategoryNN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<ProductNN>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<Category>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<Product>(b =>
                {
                    b.Property(e => e.Id1).ValueGeneratedOnAdd();
                    b.Property(e => e.Id2).ValueGeneratedOnAdd();
                });

                modelBuilder.Entity<Item>(b => { b.Property(e => e.Id).ValueGeneratedOnAdd(); });

                modelBuilder.Entity<Game>(b => { b.Property(e => e.Id).ValueGeneratedOnAdd(); });
            }
        }
    }
}