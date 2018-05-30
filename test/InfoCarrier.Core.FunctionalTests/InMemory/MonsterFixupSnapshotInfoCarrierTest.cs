// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestModels;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class MonsterFixupSnapshotInfoCarrierTest : MonsterFixupTestBase<MonsterFixupSnapshotInfoCarrierTest.TestFixture>
    {
        public MonsterFixupSnapshotInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : MonsterFixupSnapshotFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    typeof(SnapshotMonsterContext),
                    this.OnModelCreating);

            protected override void OnModelCreating<TMessage, TProduct, TProductPhoto, TProductReview, TComputerDetail, TDimensions>(
                ModelBuilder builder)
            {
                base.OnModelCreating<TMessage, TProduct, TProductPhoto, TProductReview, TComputerDetail, TDimensions>(builder);

                builder.Entity<TMessage>().Property(e => e.MessageId).ValueGeneratedOnAdd();
                builder.Entity<TProductPhoto>().Property(e => e.PhotoId).ValueGeneratedOnAdd();
                builder.Entity<TProductReview>().Property(e => e.ReviewId).ValueGeneratedOnAdd();
            }
        }
    }
}
