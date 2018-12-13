// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;

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

        [ConditionalTheory(Skip = "https://github.com/6bee/aqua-core/issues/27")]
        public override void Include_collection_hiding_props(bool tracking)
        {
            base.Include_collection_hiding_props(tracking);
        }

        [ConditionalTheory(Skip = "https://github.com/6bee/aqua-core/issues/27")]
        public override void Include_reference_hiding_props(bool tracking)
        {
            base.Include_reference_hiding_props(tracking);
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
