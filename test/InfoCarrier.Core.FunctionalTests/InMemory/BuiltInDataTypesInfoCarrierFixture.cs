// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;

    public class BuiltInDataTypesInfoCarrierFixture : BuiltInDataTypesFixtureBase
    {
        private readonly InfoCarrierTestHelper<DbContext> helper;
        private readonly TestStoreBase testStore;

        public BuiltInDataTypesInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<DbContext>.CreateHelper(
                this.OnModelCreating,
                opt => new DbContext(opt),
                _ => { });

            this.testStore = this.helper.CreateTestStore();
        }

        public override bool SupportsBinaryKeys
            => false;

        public override DateTime DefaultDateTime
            => default(DateTime);

        public override DbContext CreateContext()
            => this.helper.CreateInfoCarrierContext(this.testStore);

        public override void Dispose()
        {
            this.testStore.Dispose();
        }
    }
}
