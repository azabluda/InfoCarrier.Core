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

        // Skipped: Owned entity navigation loading through Remote.Linq has expression
        // translation limitations. See MIGRATION_STATUS.md Cat 8.
        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: Owned entity projection not fully supported. See MIGRATION_STATUS.md Cat 8")]
        public override Task Unmapped_property_projection_loads_owned_navigations(bool async)
            => base.Unmapped_property_projection_loads_owned_navigations(async);

        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: Owned entity projection not fully supported. See MIGRATION_STATUS.md Cat 8")]
        public override Task Project_multiple_owned_navigations(bool async)
            => base.Project_multiple_owned_navigations(async);

        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: Owned entity with indexer property not supported. See MIGRATION_STATUS.md Cat 8")]
        public override Task Projecting_indexer_property_ignores_include(bool async)
            => base.Projecting_indexer_property_ignores_include(async);

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
