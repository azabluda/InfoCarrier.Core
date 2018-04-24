// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.SqlServer
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;
    using Xunit;

    [Collection("SqlServer")]
    public class GraphUpdatesInfoCarrierTest
        : GraphUpdatesTestBase<GraphUpdatesInfoCarrierTest.TestFixture>
    {
        public GraphUpdatesInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : GraphUpdatesFixtureBase
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
                modelBuilder.ForSqlServerUseSequenceHiLo(); // ensure model uses sequences

                base.OnModelCreating(modelBuilder, context);
            }
        }
    }
}
