// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.SqlServer
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestModels.ConcurrencyModel;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;
    using Xunit;

    [Collection("SqlServer")]
    public class OptimisticConcurrencyInfoCarrierTest
        : OptimisticConcurrencyTestBase<OptimisticConcurrencyInfoCarrierTest.TestFixture>
    {
        public OptimisticConcurrencyInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : F1RelationalFixture
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.CreateOrGet(
                    ref this.testStoreFactory,
                    this.ContextType,
                    this.OnModelCreating,
                    InfoCarrierTestStoreFactory.SqlServer);

            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                base.OnModelCreating(modelBuilder, context);

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
