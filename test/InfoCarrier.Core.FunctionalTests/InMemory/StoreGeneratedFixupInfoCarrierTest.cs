// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;

    public class StoreGeneratedFixupInfoCarrierTest : StoreGeneratedFixupTestBase<StoreGeneratedFixupInfoCarrierTest.TestFixture>
    {
        public StoreGeneratedFixupInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        protected override bool EnforcesFKs => false;

        protected override void ExecuteWithStrategyInTransaction(Action<DbContext> testOperation)
        {
            base.ExecuteWithStrategyInTransaction(testOperation);
            this.Fixture.Reseed();
        }

        public class TestFixture : StoreGeneratedFixupFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.CreateOrGet(
                    ref this.testStoreFactory,
                    this.ContextType,
                    this.OnModelCreating,
                    InfoCarrierTestStoreFactory.InMemory,
                    o => o.ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning)));

            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                base.OnModelCreating(modelBuilder, context);

                modelBuilder.Entity<Parent>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<Child>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<ParentPN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<ChildPN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<ParentDN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<ChildDN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<ParentNN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<ChildNN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<CategoryDN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<ProductDN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<CategoryPN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<ProductPN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<CategoryNN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<ProductNN>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<Category>(
                    b =>
                    {
                        b.Property(e => e.Id1).ValueGeneratedOnAdd();
                        b.Property(e => e.Id2).ValueGeneratedOnAdd();
                    });

                modelBuilder.Entity<Product>(
                    b =>
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
