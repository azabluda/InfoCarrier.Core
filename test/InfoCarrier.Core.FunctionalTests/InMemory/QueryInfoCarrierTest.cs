namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;
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
