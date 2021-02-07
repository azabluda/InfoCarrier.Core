// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class OwnedQueryInfoCarrierTest : OwnedQueryTestBase<OwnedQueryInfoCarrierTest.TestFixture>
    {
        public OwnedQueryInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "issue #19742")]
        public override Task Projecting_collection_correlated_with_keyless_entity_after_navigation_works_using_parent_identifiers(bool async)
        {
            return base.Projecting_collection_correlated_with_keyless_entity_after_navigation_works_using_parent_identifiers(async);
        }

        public class TestFixture : OwnedQueryFixtureBase
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
