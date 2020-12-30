// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;
    using Xunit.Abstractions;

    public class ComplexNavigationsWeakQueryInfoCarrierTest :
        ComplexNavigationsWeakQueryTestBase<ComplexNavigationsWeakQueryInfoCarrierTest.TestFixture>
    {
        public ComplexNavigationsWeakQueryInfoCarrierTest(TestFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Complex_query_with_optional_navigations_and_client_side_evaluation(bool isAsync)
        {
            return base.Complex_query_with_optional_navigations_and_client_side_evaluation(isAsync);
        }

        [ConditionalTheory(Skip = "17539")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Join_navigations_in_inner_selector_translated_without_collision(bool isAsync)
        {
            return base.Join_navigations_in_inner_selector_translated_without_collision(isAsync);
        }

        [ConditionalTheory(Skip = "17539")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Join_with_navigations_in_the_result_selector1(bool isAsync)
        {
            return base.Join_with_navigations_in_the_result_selector1(isAsync);
        }

        [ConditionalTheory(Skip = "17539")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Where_nav_prop_reference_optional1_via_DefaultIfEmpty(bool isAsync)
        {
            return base.Where_nav_prop_reference_optional1_via_DefaultIfEmpty(isAsync);
        }

        [ConditionalTheory(Skip = "17539")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Optional_navigation_propagates_nullability_to_manually_created_left_join2(bool isAsync)
        {
            return base.Optional_navigation_propagates_nullability_to_manually_created_left_join2(isAsync);
        }

        [ConditionalTheory(Skip = "issue #17620")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Lift_projection_mapping_when_pushing_down_subquery(bool isAsync)
        {
            return base.Lift_projection_mapping_when_pushing_down_subquery(isAsync);
        }

        [ConditionalTheory(Skip = "issue #18912")]
        [MemberData(nameof(IsAsyncData))]
        public override Task OrderBy_collection_count_ThenBy_reference_navigation(bool async)
        {
            return base.OrderBy_collection_count_ThenBy_reference_navigation(async);
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
