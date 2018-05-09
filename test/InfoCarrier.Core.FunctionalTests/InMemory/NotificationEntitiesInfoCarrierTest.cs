// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;

    public class NotificationEntitiesInfoCarrierTest : NotificationEntitiesTestBase<NotificationEntitiesInfoCarrierTest.TestFixture>
    {
        public NotificationEntitiesInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : NotificationEntitiesFixtureBase
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
