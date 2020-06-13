// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;
    using Xunit.Abstractions;

    public class SimpleQueryInfoCarrierTest : SimpleQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public SimpleQueryInfoCarrierTest(
            NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture,
            ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        public override void Where_navigation_contains()
        {
            using (var context = this.CreateContext())
            {
                var customer = context.Customers.Include(c => c.Orders).Single(c => c.CustomerID == "ALFKI");
                customer.Context = null; // Prevent Remote.Linq from serializing the entire DbContext
                var orderDetails = context.OrderDetails.Where(od => customer.Orders.Contains(od.Order)).ToList();

                Assert.Equal(12, orderDetails.Count);
            }
        }

        // InMemory can throw server side exception
        public override void Average_no_data_subquery()
        {
            Assert.Throws<InvalidOperationException>(() => base.Average_no_data_subquery());
        }

        public override void Max_no_data_subquery()
        {
            Assert.Throws<InvalidOperationException>(() => base.Max_no_data_subquery());
        }

        public override void Min_no_data_subquery()
        {
            Assert.Throws<InvalidOperationException>(() => base.Min_no_data_subquery());
        }

        public override Task Where_query_composition_entity_equality_one_element_Single(bool isAsync)
        {
            return Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_one_element_Single(isAsync));
        }

        public override Task Where_query_composition_entity_equality_one_element_First(bool isAsync)
        {
            return Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_one_element_First(isAsync));
        }

        public override Task Where_query_composition_entity_equality_no_elements_Single(bool isAsync)
        {
            return Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_no_elements_Single(isAsync));
        }

        public override Task Where_query_composition_entity_equality_no_elements_First(bool isAsync)
        {
            return Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_no_elements_First(isAsync));
        }

        public override Task Where_query_composition_entity_equality_multiple_elements_SingleOrDefault(bool isAsync)
        {
            return Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_multiple_elements_SingleOrDefault(isAsync));
        }

        public override Task Where_query_composition_entity_equality_multiple_elements_Single(bool isAsync)
        {
            return Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_multiple_elements_Single(isAsync));
        }

        public override Task Collection_Last_member_access_in_projection_translated(bool isAsync)
        {
            return Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Collection_Last_member_access_in_projection_translated(isAsync));
        }

        // Sending client code to server
        [ConditionalFact(Skip = "Issue#17050")]
        public override void Client_code_using_instance_in_anonymous_type()
        {
            base.Client_code_using_instance_in_anonymous_type();
        }

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Client_code_using_instance_in_static_method()
        {
            base.Client_code_using_instance_in_static_method();
        }

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Client_code_using_instance_method_throws()
        {
            base.Client_code_using_instance_method_throws();
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Contains_with_local_tuple_array_closure(bool isAsync)
        {
            return base.Contains_with_local_tuple_array_closure(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Last_when_no_order_by(bool isAsync)
        {
            return base.Last_when_no_order_by(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task OrderBy_multiple_queries(bool isAsync)
        {
            return base.OrderBy_multiple_queries(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Random_next_is_not_funcletized_1(bool isAsync)
        {
            return base.Random_next_is_not_funcletized_1(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Random_next_is_not_funcletized_2(bool isAsync)
        {
            return base.Random_next_is_not_funcletized_2(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Random_next_is_not_funcletized_3(bool isAsync)
        {
            return base.Random_next_is_not_funcletized_3(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Random_next_is_not_funcletized_4(bool isAsync)
        {
            return base.Random_next_is_not_funcletized_4(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Random_next_is_not_funcletized_5(bool isAsync)
        {
            return base.Random_next_is_not_funcletized_5(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Random_next_is_not_funcletized_6(bool isAsync)
        {
            return base.Random_next_is_not_funcletized_6(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Select_bool_closure_with_order_by_property_with_cast_to_nullable(bool isAsync)
        {
            return base.Select_bool_closure_with_order_by_property_with_cast_to_nullable(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Where_bool_client_side_negated(bool isAsync)
        {
            return base.Where_bool_client_side_negated(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Projection_when_arithmetic_mixed_subqueries(bool isAsync)
        {
            return base.Projection_when_arithmetic_mixed_subqueries(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Where_equals_method_string_with_ignore_case(bool isAsync)
        {
            return base.Where_equals_method_string_with_ignore_case(isAsync);
        }

        [ConditionalTheory(Skip = "Issue#17536")]
        [MemberData(nameof(IsAsyncData))]
        public override Task SelectMany_correlated_with_outer_3(bool isAsync)
        {
            return base.SelectMany_correlated_with_outer_3(isAsync);
        }

        [ConditionalTheory]
        [MemberData(nameof(IsAsyncData))]
        public override Task DefaultIfEmpty_in_subquery_nested(bool isAsync)
        {
            return base.DefaultIfEmpty_in_subquery_nested(isAsync);
        }

        [ConditionalTheory(Skip = "issue #17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Where_equals_on_null_nullable_int_types(bool isAsync)
        {
            return base.Where_equals_on_null_nullable_int_types(isAsync);
        }

        // Casting int to object to string is invalid for InMemory
        public override Task Like_with_non_string_column_using_double_cast(bool isAsync) => Task.CompletedTask;

        [ConditionalTheory(Skip = "Self referencing loop because of Customer.Context property. Not supported.")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Context_based_client_method(bool isAsync)
        {
            return base.Context_based_client_method(isAsync);
        }

        [ConditionalFact(Skip = "Concurrency detection mechanism cannot be used")]
        public override void Throws_on_concurrent_query_list()
        {
            base.Throws_on_concurrent_query_list();
        }

        [ConditionalFact(Skip = "Concurrency detection mechanism cannot be used")]
        public override void Throws_on_concurrent_query_first()
        {
            base.Throws_on_concurrent_query_first();
        }

        //[ConditionalTheory(Skip = "Client-side evaluation is not supported")]
        //public override Task Client_OrderBy_GroupBy_Group_ordering_works(bool isAsync)
        //{
        //    return base.Client_OrderBy_GroupBy_Group_ordering_works(isAsync);
        //}
    }
}
