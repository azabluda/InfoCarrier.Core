// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;

    public class OneToOneQueryInfoCarrierFixture : OneToOneQueryFixtureBase, IDisposable
    {
        private readonly InfoCarrierTestHelper<DbContext> helper;
        private readonly TestStoreBase testStore;

        public OneToOneQueryInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<DbContext>.CreateHelper(
                this.OnModelCreating,
                opt => new DbContext(opt),
                AddTestData);

            this.testStore = this.helper.CreateTestStore();
        }

        public DbContext CreateContext()
            => this.helper.CreateInfoCarrierContext(this.testStore);

        public void Dispose()
        {
            this.testStore.Dispose();
        }
    }
}
