namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
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

        [Fact(Skip = "https://github.com/aspnet/EntityFramework/issues/4311#issuecomment-278652479")]
        public override void OfType_Select()
        {
            base.OfType_Select();
        }

        [Fact(Skip = "https://github.com/aspnet/EntityFramework/issues/4311#issuecomment-278652479")]
        public override void OfType_Select_OfType_Select()
        {
            base.OfType_Select_OfType_Select();
        }

        [Fact(Skip = "Client-side evaluation not fully supported")]
        public override void Client_Join_select_many()
        {
            base.Client_Join_select_many();
        }
    }
}
