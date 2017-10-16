// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;

    public class LoadInfoCarrierTest
        : LoadTestBase<TestStoreBase, LoadInfoCarrierTest.LoadInfoCarrierFixture>
    {
        public LoadInfoCarrierTest(LoadInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class LoadInfoCarrierFixture : LoadFixtureBase
        {
            private readonly InfoCarrierTestHelper<LoadContext> helper;

            public LoadInfoCarrierFixture()
            {
                this.helper = InMemoryTestStore<LoadContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new LoadContext(opt),
                    this.Seed);
            }

            public override DbContext CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();
        }
    }
}
