namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using System.Linq;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Northwind;
    using Xunit;

    public class QueryInfoCarrierTest : QueryTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public QueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "https://github.com/aspnet/EntityFramework/issues/4311")]
        public override void GroupJoin_customers_orders_count_preserves_ordering()
        {
            base.GroupJoin_customers_orders_count_preserves_ordering();
        }

        [Fact(Skip = "https://github.com/aspnet/EntityFramework/issues/4311")]
        public override void GroupJoin_DefaultIfEmpty3()
        {
            base.GroupJoin_DefaultIfEmpty3();
        }

        [Fact(Skip = "Client-side evaluation not fully supported")]
        public override void Client_Join_select_many()
        {
            base.Client_Join_select_many();
        }

        [Fact]
        public override void Take_with_single_select_many()
        {
            // UGLY: this is a complete copy-n-paste of the original test.
            // Workarounds an unexplainable failure when running the test with netcoreapp1.0.
            AssertQuery<Customer, Order>((cs, os) =>
                (from c in cs
                    from o in os
                    orderby c.CustomerID, o.OrderID
                    select new { c, o })
                .Take(1)
                .Cast<object>()
                .Single());

            void AssertQuery<TItem1, TItem2>(
                Func<IQueryable<TItem1>, IQueryable<TItem2>, object> query,
                bool assertOrder = false)
                where TItem1 : class
                where TItem2 : class
            {
                using (var context = this.CreateContext())
                {
                    TestHelpers.AssertResults(
                        new[] { query(NorthwindData.Set<TItem1>(), NorthwindData.Set<TItem2>()) },
                        new[] { query(context.Set<TItem1>(), context.Set<TItem2>()) },
                        assertOrder);
                }
            }
        }

        [Fact(Skip = "Revisit after https://github.com/aspnet/EntityFrameworkCore/issues/9301")]
        public override void GroupJoin_outer_projection2()
        {
            base.GroupJoin_outer_projection2();
        }

        [Fact(Skip = "Revisit after https://github.com/aspnet/EntityFrameworkCore/issues/9301")]
        public override void GroupJoin_outer_projection3()
        {
            base.GroupJoin_outer_projection3();
        }

        [Fact(Skip = "Revisit after https://github.com/aspnet/EntityFrameworkCore/issues/9301")]
        public override void GroupJoin_outer_projection4()
        {
            base.GroupJoin_outer_projection4();
        }
    }
}
