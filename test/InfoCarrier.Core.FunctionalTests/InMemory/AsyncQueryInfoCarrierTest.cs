namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Northwind;
    using Xunit;

    public class AsyncQueryInfoCarrierTest : AsyncQueryTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public AsyncQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "Not valid for in-memory (from AsyncQueryInMemoryTest)")]
        public override Task ToList_on_nav_in_projection_is_async()
        {
            return base.ToList_on_nav_in_projection_is_async();
        }

        [Fact(Skip = "https://github.com/aspnet/EntityFramework/issues/9301")]
        public override Task Mixed_sync_async_query()
        {
            return base.Mixed_sync_async_query();
        }

        [Fact]
        public override async Task Take_with_single_select_many()
        {
            // UGLY: this is a complete copy-n-paste of the original test.
            // Workarounds an unexplainable failure when running the test with netcoreapp1.0.
            await AssertQuery<Customer, Order>((cs, os) =>
                (from c in cs
                    from o in os
                    orderby c.CustomerID, o.OrderID
                    select new { c, o })
                .Take(1)
                .Cast<object>()
                .SingleAsync());

            async Task AssertQuery<TItem1, TItem2>(
                Func<IQueryable<TItem1>, IQueryable<TItem2>, Task<object>> query,
                bool assertOrder = false)
                where TItem1 : class
                where TItem2 : class
            {
                using (var context = this.CreateContext())
                {
                    TestHelpers.AssertResults(
                        new[] { await query(NorthwindData.Set<TItem1>(), NorthwindData.Set<TItem2>()) },
                        new[] { await query(context.Set<TItem1>(), context.Set<TItem2>()) },
                        assertOrder);
                }
            }
        }
    }
}
