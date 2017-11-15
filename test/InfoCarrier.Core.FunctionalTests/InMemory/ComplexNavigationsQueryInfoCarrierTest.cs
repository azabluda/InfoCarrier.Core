// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;

    public class ComplexNavigationsQueryInfoCarrierTest : ComplexNavigationsQueryTestBase<TestStoreBase, ComplexNavigationsQueryInfoCarrierFixture>
    {
        public ComplexNavigationsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "issue #4311 (from ComplexNavigationsQueryInMemoryTest)")]
        public override void Nested_group_join_with_take()
        {
            base.Nested_group_join_with_take();
        }

        [Fact(Skip = "issue #9591 (from ComplexNavigationsQueryInMemoryTest)")]
        public override void Multi_include_with_groupby_in_subquery()
        {
            base.Multi_include_with_groupby_in_subquery();
        }

        [Fact(Skip = "Issue #10060 (from ComplexNavigationsQueryInMemoryTest)")]
        public override void Include_reference_collection_order_by_reference_navigation()
        {
            base.Include_reference_collection_order_by_reference_navigation();
        }

        [Fact(Skip = "Client-side evaluation not fully supported")]
        public override void Complex_query_with_optional_navigations_and_client_side_evaluation()
        {
            base.Complex_query_with_optional_navigations_and_client_side_evaluation();
        }

        [Fact(Skip = "Client-side evaluation not fully supported")]
        public override void Null_reference_protection_complex_client_eval()
        {
            base.Null_reference_protection_complex_client_eval();
        }
    }
}
