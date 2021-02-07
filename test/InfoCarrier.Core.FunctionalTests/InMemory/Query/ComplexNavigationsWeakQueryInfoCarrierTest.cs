// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class ComplexNavigationsWeakQueryInfoCarrierTest :
        ComplexNavigationsWeakQueryTestBase<ComplexNavigationsWeakQueryInfoCarrierTest.TestFixture>
    {
        public ComplexNavigationsWeakQueryInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Complex_query_with_optional_navigations_and_client_side_evaluation(bool async)
        {
            return base.Complex_query_with_optional_navigations_and_client_side_evaluation(async);
        }

        [ConditionalTheory(Skip = "Issue#17539")]
        public override Task Join_navigations_in_inner_selector_translated_without_collision(bool async)
        {
            return base.Join_navigations_in_inner_selector_translated_without_collision(async);
        }

        [ConditionalTheory(Skip = "Issue#17539")]
        public override Task Join_with_navigations_in_the_result_selector1(bool async)
        {
            return base.Join_with_navigations_in_the_result_selector1(async);
        }

        [ConditionalTheory(Skip = "Issue#17539")]
        public override Task Where_nav_prop_reference_optional1_via_DefaultIfEmpty(bool async)
        {
            return base.Where_nav_prop_reference_optional1_via_DefaultIfEmpty(async);
        }

        [ConditionalTheory(Skip = "Issue#17539")]
        public override Task Where_nav_prop_reference_optional2_via_DefaultIfEmpty(bool async)
        {
            return base.Where_nav_prop_reference_optional2_via_DefaultIfEmpty(async);
        }

        [ConditionalTheory(Skip = "Issue#17539")]
        public override Task Optional_navigation_propagates_nullability_to_manually_created_left_join2(bool async)
        {
            return base.Optional_navigation_propagates_nullability_to_manually_created_left_join2(async);
        }

        [ConditionalTheory(Skip = "issue #17620")]
        public override Task Lift_projection_mapping_when_pushing_down_subquery(bool async)
        {
            return base.Lift_projection_mapping_when_pushing_down_subquery(async);
        }

        [ConditionalTheory(Skip = "issue #18912")]
        public override Task OrderBy_collection_count_ThenBy_reference_navigation(bool async)
        {
            return base.OrderBy_collection_count_ThenBy_reference_navigation(async);
        }

        [ConditionalTheory(Skip = "issue #19344")]
        public override Task Select_subquery_single_nested_subquery(bool async)
        {
            return base.Select_subquery_single_nested_subquery(async);
        }

        [ConditionalTheory(Skip = "issue #19344")]
        public override Task Select_subquery_single_nested_subquery2(bool async)
        {
            return base.Select_subquery_single_nested_subquery2(async);
        }

        [ConditionalTheory(Skip = "issue #19967")]
        public override Task SelectMany_with_outside_reference_to_joined_table_correctly_translated_to_apply(bool async)
        {
            return base.SelectMany_with_outside_reference_to_joined_table_correctly_translated_to_apply(async);
        }

        [ConditionalTheory(Skip = "issue #19967")]
        public override Task Nested_SelectMany_correlated_with_join_table_correctly_translated_to_apply(bool async)
        {
            return base.Nested_SelectMany_correlated_with_join_table_correctly_translated_to_apply(async);
        }

        [ConditionalTheory(Skip = "issue #19742")]
        public override Task Contains_over_optional_navigation_with_null_column(bool async)
        {
            return base.Contains_over_optional_navigation_with_null_column(async);
        }

        [ConditionalTheory(Skip = "issue #19742")]
        public override Task Contains_over_optional_navigation_with_null_entity_reference(bool async)
        {
            return base.Contains_over_optional_navigation_with_null_entity_reference(async);
        }

        public class TestFixture : ComplexNavigationsWeakQueryFixtureBase
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
