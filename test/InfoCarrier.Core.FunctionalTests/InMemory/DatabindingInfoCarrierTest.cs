// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;

    public class DatabindingInfoCarrierTest : DatabindingTestBase<DatabindingInfoCarrierTest.F1InfoCarrierFixture>
    {
        public DatabindingInfoCarrierTest(F1InfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public class F1InfoCarrierFixture : F1FixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating);
        }
    }
}
