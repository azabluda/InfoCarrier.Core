// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Northwind;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindMiscellaneousQueryInfoCarrierTest :
        NorthwindMiscellaneousQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindMiscellaneousQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        public override Task Where_query_composition_entity_equality_one_element_Single(bool async)
            => Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_one_element_Single(async));

        public override Task Where_query_composition_entity_equality_one_element_First(bool async)
            => Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_one_element_First(async));

        public override Task Where_query_composition_entity_equality_no_elements_Single(bool async)
            => Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_no_elements_Single(async));

        public override Task Where_query_composition_entity_equality_no_elements_First(bool async)
            => Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_no_elements_First(async));

        public override Task Where_query_composition_entity_equality_multiple_elements_SingleOrDefault(bool async)
            => Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_multiple_elements_SingleOrDefault(async));

        public override Task Where_query_composition_entity_equality_multiple_elements_Single(bool async)
            => Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Where_query_composition_entity_equality_multiple_elements_Single(async));

        // Sending client code to server
        [ConditionalFact(Skip = "Issue#17050")]
        public override void Client_code_using_instance_in_anonymous_type()
            => base.Client_code_using_instance_in_anonymous_type();

        [ConditionalTheory(Skip = "Issue#17050")]
        public override Task Client_code_unknown_method(bool async)
            => base.Client_code_unknown_method(async);

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Client_code_using_instance_in_static_method()
            => base.Client_code_using_instance_in_static_method();

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Client_code_using_instance_method_throws()
            => base.Client_code_using_instance_method_throws();

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task OrderBy_multiple_queries(bool async)
            => base.OrderBy_multiple_queries(async);

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Random_next_is_not_funcletized_1(bool async)
            => base.Random_next_is_not_funcletized_1(async);

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Random_next_is_not_funcletized_2(bool async)
            => base.Random_next_is_not_funcletized_2(async);

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Random_next_is_not_funcletized_3(bool async)
            => base.Random_next_is_not_funcletized_3(async);

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Random_next_is_not_funcletized_4(bool async)
            => base.Random_next_is_not_funcletized_4(async);

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Random_next_is_not_funcletized_5(bool async)
            => base.Random_next_is_not_funcletized_5(async);

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Random_next_is_not_funcletized_6(bool async)
            => base.Random_next_is_not_funcletized_6(async);

        [ConditionalTheory(Skip = "issue#17386")]
        public override Task Where_query_composition5(bool async)
            => base.Where_query_composition5(async);

        [ConditionalTheory(Skip = "issue#17386")]
        public override Task Where_query_composition6(bool async)
            => base.Where_query_composition6(async);

        [ConditionalTheory(Skip = "issue #17386")]
        public override Task Using_string_Equals_with_StringComparison_throws_informative_error(bool async)
            => base.Using_string_Equals_with_StringComparison_throws_informative_error(async);

        [ConditionalTheory(Skip = "issue #17386")]
        public override Task Using_static_string_Equals_with_StringComparison_throws_informative_error(bool async)
            => base.Using_static_string_Equals_with_StringComparison_throws_informative_error(async);

        public override async Task Max_on_empty_sequence_throws(bool async)
        {
            using var context = this.CreateContext();
            var query = context.Set<Customer>().Select(e => new { Max = e.Orders.Max(o => o.OrderID) });

            var message = async
                ? (await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync())).Message
                : Assert.Throws<InvalidOperationException>(() => query.ToList()).Message;

            Assert.Equal("Sequence contains no elements", message);
        }

        // Skipped: Context-based client methods and GroupBy ordering have
        // expression translation limitations through Remote.Linq.
        // See MIGRATION_STATUS.md Cat 6.
        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: Context-based client method not supported. See MIGRATION_STATUS.md Cat 6")]
        public override Task Context_based_client_method(bool async)
            => base.Context_based_client_method(async);

        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: GroupBy with client ordering not supported. See MIGRATION_STATUS.md Cat 6")]
        public override Task Client_OrderBy_GroupBy_Group_ordering_works(bool async)
            => base.Client_OrderBy_GroupBy_Group_ordering_works(async);
    }
}
