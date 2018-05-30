// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.SqlServer
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestModels.ConcurrencyModel;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class OptimisticConcurrencyInfoCarrierTest
        : OptimisticConcurrencyTestBase<OptimisticConcurrencyInfoCarrierTest.TestFixture>
    {
        public OptimisticConcurrencyInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : F1FixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.SqlServer,
                    this.ContextType,
                    this.OnModelCreating);

            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                base.OnModelCreating(modelBuilder, context);

                modelBuilder.Entity<Team>().Property(e => e.Id).ValueGeneratedNever();

                modelBuilder.Entity<Chassis>().Property<byte[]>("Version").IsRowVersion();
                modelBuilder.Entity<Driver>().Property<byte[]>("Version").IsRowVersion();

                modelBuilder.Entity<Team>().Property<byte[]>("Version")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();

                modelBuilder.Entity<TitleSponsor>()
                    .OwnsOne(s => s.Details)
                    .Property(d => d.Space).HasColumnType("decimal(18,2)");
            }
        }
    }
}
