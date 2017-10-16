﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;

    public class PropertyValuesInfoCarrierTest
        : PropertyValuesTestBase<TestStoreBase, PropertyValuesInfoCarrierTest.PropertyValuesInfoCarrierFixture>
    {
        public PropertyValuesInfoCarrierTest(PropertyValuesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class PropertyValuesInfoCarrierFixture : PropertyValuesFixtureBase
        {
            private readonly InfoCarrierTestHelper<AdvancedPatternsMasterContext> helper;

            public PropertyValuesInfoCarrierFixture()
            {
                this.helper = InMemoryTestStore<AdvancedPatternsMasterContext>.CreateHelper(
                    this.OnModelCreating,
                    opt => new AdvancedPatternsMasterContext(opt),
                    this.Seed);
            }

            public override TestStoreBase CreateTestStore()
                => this.helper.CreateTestStore();

            public override DbContext CreateContext(TestStoreBase testStore)
                => this.helper.CreateInfoCarrierContext(testStore);
        }
    }
}