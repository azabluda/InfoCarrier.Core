// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.InMemory.Internal;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;

    public class UpdatesInfoCarrierWithoutSensitiveDataLoggingTest : UpdatesInfoCarrierTestBase<UpdatesInfoCarrierWithoutSensitiveDataLoggingTest.TestFixture>
    {
        public UpdatesInfoCarrierWithoutSensitiveDataLoggingTest(TestFixture fixture)
            : base(fixture)
        {
        }

        protected override string UpdateConcurrencyTokenMessage
            => InMemoryStrings.UpdateConcurrencyTokenException("Product", "{'Price'}");

        public class TestFixture : UpdatesInfoCarrierFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating,
                    o => o.ConfigureWarnings(c => c.Log(InMemoryEventId.TransactionIgnoredWarning)).EnableSensitiveDataLogging(false));
        }
    }
}
