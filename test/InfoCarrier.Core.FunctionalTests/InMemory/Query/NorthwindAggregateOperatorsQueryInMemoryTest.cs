// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindAggregateOperatorsQueryInMemoryTest :
        NorthwindAggregateOperatorsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindAggregateOperatorsQueryInMemoryTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        // InMemory can throw server side exception
        public override void Average_no_data_subquery()
        {
            using var context = this.CreateContext();

            Assert.Equal(
                "Sequence contains no elements",
                Assert.Throws<InvalidOperationException>(
                    () => context.Customers.Select(c => c.Orders.Where(o => o.OrderID == -1).Average(o => o.OrderID)).ToList()).Message);
        }

        public override void Max_no_data_subquery()
        {
            using var context = this.CreateContext();

            Assert.Equal(
                "Sequence contains no elements",
                Assert.Throws<InvalidOperationException>(
                    () => context.Customers.Select(c => c.Orders.Where(o => o.OrderID == -1).Max(o => o.OrderID)).ToList()).Message);
        }

        public override void Min_no_data_subquery()
        {
            using var context = this.CreateContext();

            Assert.Equal(
                "Sequence contains no elements",
                Assert.Throws<InvalidOperationException>(
                    () => context.Customers.Select(c => c.Orders.Where(o => o.OrderID == -1).Min(o => o.OrderID)).ToList()).Message);
        }

        public override Task Collection_Last_member_access_in_projection_translated(bool async)
        {
            return Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Collection_Last_member_access_in_projection_translated(async));
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Contains_with_local_tuple_array_closure(bool async)
        {
            return base.Contains_with_local_tuple_array_closure(async);
        }

        [ConditionalFact(Skip = "Issue#20023")]
        public override void Contains_over_keyless_entity_throws()
        {
            base.Contains_over_keyless_entity_throws();
        }

        // Skipped: Contains with local collections has expression translation
        // limitations through Remote.Linq pipeline.
        // See MIGRATION_STATUS.md Cat 5.
        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: Contains with local collections not fully supported. See MIGRATION_STATUS.md Cat 5")]
        public override Task Contains_with_local_non_primitive_list_closure_mix(bool async)
            => base.Contains_with_local_non_primitive_list_closure_mix(async);

        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: Contains with local collections not fully supported. See MIGRATION_STATUS.md Cat 5")]
        public override Task Contains_with_local_non_primitive_list_inline_closure_mix(bool async)
            => base.Contains_with_local_non_primitive_list_inline_closure_mix(async);

        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: ImmutableHashSet Contains not supported through Remote.Linq. See MIGRATION_STATUS.md Cat 5")]
        public override Task ImmutableHashSet_Contains_with_parameter(bool async)
            => base.ImmutableHashSet_Contains_with_parameter(async);
    }
}
