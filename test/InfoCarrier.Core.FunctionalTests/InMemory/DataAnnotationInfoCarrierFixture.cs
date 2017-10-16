// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;

    public class DataAnnotationInfoCarrierFixture : DataAnnotationFixtureBase<TestStoreBase>
    {
        private readonly InfoCarrierTestHelper<DataAnnotationContext> helper;

        public DataAnnotationInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<DataAnnotationContext>.CreateHelper(
                this.OnModelCreating,
                opt => new DataAnnotationContext(opt),
                DataAnnotationModelInitializer.Seed,
                true,
                w =>
                {
                    w.Default(WarningBehavior.Throw);
                    w.Ignore(InMemoryEventId.TransactionIgnoredWarning);
                });
        }

        public override TestStoreBase CreateTestStore()
            => this.helper.CreateTestStore();

        public override DataAnnotationContext CreateContext(TestStoreBase testStore)
            => this.helper.CreateInfoCarrierContext(testStore);
    }
}
