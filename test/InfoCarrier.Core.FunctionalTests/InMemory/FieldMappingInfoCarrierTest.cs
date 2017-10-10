// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;

    public class FieldMappingInfoCarrierTest
        : FieldMappingTestBase<TestStoreBase, FieldMappingInfoCarrierTest.FieldMappingInfoCarrierFixture>
    {
        public FieldMappingInfoCarrierTest(FieldMappingInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class FieldMappingInfoCarrierFixture : FieldMappingFixtureBase
        {
            private readonly InfoCarrierTestHelper<FieldMappingContext> helper;

            public FieldMappingInfoCarrierFixture()
            {
                this.helper = InMemoryTestStore<FieldMappingContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new FieldMappingContext(opt),
                    this.Seed,
                    false,
                    w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            }

            public override DbContext CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();
        }
    }
}
