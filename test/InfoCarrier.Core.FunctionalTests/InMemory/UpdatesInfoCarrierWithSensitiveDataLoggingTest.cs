// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.InMemory.Internal;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;

    public class UpdatesInfoCarrierWithSensitiveDataLoggingTest : UpdatesInfoCarrierTestBase<UpdatesInfoCarrierWithSensitiveDataLoggingTest.TestFixture>
    {
        public UpdatesInfoCarrierWithSensitiveDataLoggingTest(TestFixture fixture)
            : base(fixture)
        {
        }

        protected override string UpdateConcurrencyTokenMessage
            => InMemoryStrings.UpdateConcurrencyTokenExceptionSensitive("Product", "{Id: 984ade3c-2f7b-4651-a351-642e92ab7146}", $"{{Price: {3.49}}}", $"{{Price: {1.49}}}");

        public class TestFixture : UpdatesInfoCarrierFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating,
                    o => o.ConfigureWarnings(c => c.Log(InMemoryEventId.TransactionIgnoredWarning)).EnableSensitiveDataLogging());
        }
    }
}
