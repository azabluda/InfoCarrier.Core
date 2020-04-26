// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
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
