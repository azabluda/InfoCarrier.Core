// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.SqlServer
{
    using Microsoft.EntityFrameworkCore;
    using Xunit;

    [Collection("SqlServer")]
    public class GraphUpdatesInfoCarrierTest
        : GraphUpdatesTestBase<TestStoreBase, GraphUpdatesInfoCarrierTest.GraphUpdatesInfoCarrierFixture>
    {
        public GraphUpdatesInfoCarrierTest(GraphUpdatesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class GraphUpdatesInfoCarrierFixture : GraphUpdatesFixtureBase
        {
            private readonly InfoCarrierTestHelper<GraphUpdatesContext> helper;

            public GraphUpdatesInfoCarrierFixture()
            {
                this.helper = SqlServerTestStore<GraphUpdatesContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new GraphUpdatesContext(opt),
                    this.Seed,
                    true,
                    "GraphSequenceUpdatesTest");
            }

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();

            public override DbContext CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.ForSqlServerUseSequenceHiLo(); // ensure model uses sequences

                base.OnModelCreating(modelBuilder);
            }
        }
    }
}
