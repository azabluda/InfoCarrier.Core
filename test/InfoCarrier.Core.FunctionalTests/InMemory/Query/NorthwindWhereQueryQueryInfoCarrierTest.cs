// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindWhereQueryQueryInfoCarrierTest :
        NorthwindWhereQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindWhereQueryQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
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

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Where_bool_client_side_negated(bool async)
        {
            return base.Where_bool_client_side_negated(async);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Where_equals_method_string_with_ignore_case(bool async)
        {
            return base.Where_equals_method_string_with_ignore_case(async);
        }

        [ConditionalTheory(Skip = "issue #17386")]
        public override Task Where_equals_on_null_nullable_int_types(bool async)
        {
            return base.Where_equals_on_null_nullable_int_types(async);
        }

        public override async Task<string> Where_simple_closure(bool async)
        {
            var queryString = await base.Where_simple_closure(async);

            Assert.Equal(CoreStrings.NotQueryingEnumerable, queryString);

            return null;
        }

        // Casting int to object to string is invalid for InMemory
        public override Task Like_with_non_string_column_using_double_cast(bool async)
            => Task.CompletedTask;
    }
}
