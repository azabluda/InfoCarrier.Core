// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class StoreGeneratedFixupInfoCarrierTest : StoreGeneratedFixupTestBase<StoreGeneratedFixupInfoCarrierTest.TestFixture>
    {
        public StoreGeneratedFixupInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        protected override bool EnforcesFKs => false;

        public override void Temporary_value_equals_database_generated_value()
        {
            // In-memory doesn't use real store-generated values.
        }

        protected override void ExecuteWithStrategyInTransaction(Action<DbContext> testOperation)
        {
            base.ExecuteWithStrategyInTransaction(testOperation);
            this.Fixture.Reseed();
        }

        public class TestFixture : StoreGeneratedFixupFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating,
                    o => o.ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning)));

            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                base.OnModelCreating(modelBuilder, context);

                modelBuilder.Entity<Parent>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<Child>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ParentPN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ChildPN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ParentDN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ChildDN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ParentNN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ChildNN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<CategoryDN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ProductDN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<CategoryPN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ProductPN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<CategoryNN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ProductNN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<Category>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<Product>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedNever();
                        b.Property(e => e.Id2).ValueGeneratedNever();
                    });

                modelBuilder.Entity<Item>(b => b.Property(e => e.Id).ValueGeneratedNever());

                modelBuilder.Entity<Game>(b => b.Property(e => e.Id).ValueGeneratedNever());
            }
        }
    }
}
