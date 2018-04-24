// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;

    public class LoadInfoCarrierTest : LoadTestBase<LoadInfoCarrierTest.TestFixture>
    {
        public LoadInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : LoadFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.CreateOrGet(
                    ref this.testStoreFactory,
                    this.ContextType,
                    this.OnModelCreating,
                    InfoCarrierTestStoreFactory.InMemory);
        }
    }
}
