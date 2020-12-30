// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class FieldMappingInfoCarrierTest : FieldMappingTestBase<FieldMappingInfoCarrierTest.TestFixture>
    {
        public FieldMappingInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        protected override void Update<TBlog>(string navigation)
        {
            base.Update<TBlog>(navigation);

            this.Fixture.Reseed();
        }

        public class TestFixture : FieldMappingFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating,
                    o => o.ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning)));
        }
    }
}
