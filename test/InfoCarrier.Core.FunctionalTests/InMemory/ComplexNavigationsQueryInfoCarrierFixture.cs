// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.ComplexNavigationsModel;

    public class ComplexNavigationsQueryInfoCarrierFixture : ComplexNavigationsQueryFixtureBase<TestStoreBase>
    {
        private readonly InfoCarrierTestHelper<ComplexNavigationsContext> helper;

        public ComplexNavigationsQueryInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<ComplexNavigationsContext>.CreateHelper(
                this.OnModelCreating,
                opt => new ComplexNavigationsContext(opt),
                ctx => ComplexNavigationsModelInitializer.Seed(ctx));
        }

        public override TestStoreBase CreateTestStore()
            => this.helper.CreateTestStore();

        public override ComplexNavigationsContext CreateContext(TestStoreBase testStore)
            => this.helper.CreateInfoCarrierContext(testStore);
    }
}
