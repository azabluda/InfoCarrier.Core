// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;
    using Xunit.Abstractions;

    public class GearsOfWarQueryInfoCarrierTest : GearsOfWarQueryTestBase<GearsOfWarQueryInfoCarrierFixture>
    {
        public GearsOfWarQueryInfoCarrierTest(GearsOfWarQueryInfoCarrierFixture testFixture, ITestOutputHelper testOutputHelper)
            : base(testFixture)
        {
        }

        [ConditionalTheory(Skip = "issue #17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Correlated_collection_order_by_constant_null_of_non_mapped_type(bool isAsync)
        {
            return base.Correlated_collection_order_by_constant_null_of_non_mapped_type(isAsync);
        }

        [ConditionalTheory(Skip = "issue #17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Client_side_equality_with_parameter_works_with_optional_navigations(bool isAsync)
        {
            return base.Client_side_equality_with_parameter_works_with_optional_navigations(isAsync);
        }

        [ConditionalTheory(Skip = "issue #17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Where_coalesce_with_anonymous_types(bool isAsync)
        {
            return base.Where_coalesce_with_anonymous_types(isAsync);
        }

        [ConditionalTheory(Skip = "issue #17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Where_conditional_with_anonymous_type(bool isAsync)
        {
            return base.Where_conditional_with_anonymous_type(isAsync);
        }

        [ConditionalTheory(Skip = "issue #17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task GetValueOrDefault_on_DateTimeOffset(bool isAsync)
        {
            return base.GetValueOrDefault_on_DateTimeOffset(isAsync);
        }

        [ConditionalFact(Skip = "issue #17537")]
        [MemberData(nameof(IsAsyncData))]
        public override void Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_coalesce_result1()
        {
            base.Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_coalesce_result1();
        }

        [ConditionalFact(Skip = "issue #17537")]
        [MemberData(nameof(IsAsyncData))]
        public override void Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_coalesce_result2()
        {
            base.Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_coalesce_result2();
        }

        [ConditionalTheory(Skip = "issue #17540")]
        [MemberData(nameof(IsAsyncData))]
        public override Task
            Null_semantics_is_correctly_applied_for_function_comparisons_that_take_arguments_from_optional_navigation_complex(bool isAsync)
        {
            return base.Null_semantics_is_correctly_applied_for_function_comparisons_that_take_arguments_from_optional_navigation_complex(
                isAsync);
        }

        [ConditionalTheory(Skip = "issue #18284")]
        [MemberData(nameof(IsAsyncData))]
        public override Task GroupBy_with_boolean_groupin_key_thru_navigation_access(bool isAsync)
        {
            return this.GroupBy_with_boolean_groupin_key_thru_navigation_access(isAsync);
        }

        [ConditionalTheory(Skip = "issue #17260")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Select_subquery_projecting_single_constant_inside_anonymous(bool isAsync)
        {
            return base.Select_subquery_projecting_single_constant_inside_anonymous(isAsync);
        }
    }
}
