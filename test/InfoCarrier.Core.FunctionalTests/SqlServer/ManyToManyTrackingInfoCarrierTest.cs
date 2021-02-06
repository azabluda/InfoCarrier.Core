// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.SqlServer
{
    using System.Collections.Generic;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestModels.ManyToManyModel;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class ManyToManyTrackingInfoCarrierTest : ManyToManyTrackingTestBase<ManyToManyTrackingInfoCarrierTest.TestFixture>
    {
        public ManyToManyTrackingInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : ManyToManyTrackingFixtureBase
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

                modelBuilder
                    .Entity<JoinOneSelfPayload>()
                    .Property(e => e.Payload)
                    .HasDefaultValueSql("GETUTCDATE()");

                modelBuilder
                    .SharedTypeEntity<Dictionary<string, object>>("JoinOneToThreePayloadFullShared")
                    .IndexerProperty<string>("Payload")
                    .HasDefaultValue("Generated");

                modelBuilder
                    .Entity<JoinOneToThreePayloadFull>()
                    .Property(e => e.Payload)
                    .HasDefaultValue("Generated");
            }
        }
    }
}
