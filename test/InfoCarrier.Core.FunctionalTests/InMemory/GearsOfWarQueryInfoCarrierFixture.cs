// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel;

    public class GearsOfWarQueryInfoCarrierFixture : GearsOfWarQueryFixtureBase<TestStoreBase>
    {
        private readonly InfoCarrierTestHelper<GearsOfWarContext> helper;

        public GearsOfWarQueryInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<GearsOfWarContext>.CreateHelper(
                this.OnModelCreating,
                opt => new GearsOfWarContext(opt),
                GearsOfWarModelInitializer.Seed);
        }

        public override TestStoreBase CreateTestStore()
            => this.helper.CreateTestStore();

        public override GearsOfWarContext CreateContext(TestStoreBase testStore)
        {
            var context = this.helper.CreateInfoCarrierContext(testStore);

            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            return context;
        }
    }
}
