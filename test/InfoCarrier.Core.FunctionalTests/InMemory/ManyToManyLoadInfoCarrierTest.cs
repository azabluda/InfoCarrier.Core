// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System.Threading.Tasks;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class ManyToManyLoadInfoCarrierTest : ManyToManyLoadTestBase<ManyToManyLoadInfoCarrierTest.TestFixture>
    {
        public ManyToManyLoadInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        // Skipped: Many-to-many load operations not fully supported through Remote.Linq wire protocol.
        // See MIGRATION_STATUS.md Cat 9.
        [Theory(Skip = "InfoCarrier#ManyToManyLoad: many-to-many load not fully supported through Remote.Linq. See MIGRATION_STATUS.md Cat 9")]
        [InlineData(true)]
        [InlineData(false)]
        public override Task Load_collection_using_Query_with_Include_for_same_collection(bool async)
            => base.Load_collection_using_Query_with_Include_for_same_collection(async);

        [Theory(Skip = "InfoCarrier#ManyToManyLoad: many-to-many load not fully supported through Remote.Linq. See MIGRATION_STATUS.md Cat 9")]
        [InlineData(true)]
        [InlineData(false)]
        public override Task Load_collection_using_Query_with_Include(bool async)
            => base.Load_collection_using_Query_with_Include(async);

        public class TestFixture : ManyToManyLoadFixtureBase
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
