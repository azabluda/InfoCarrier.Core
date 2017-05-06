namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class QueryNavigationsInfoCarrierTest : QueryNavigationsTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public QueryNavigationsInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "https://github.com/aspnet/EntityFramework/issues/7559")]
        public override void Select_Where_Navigation_Included()
        {
            base.Select_Where_Navigation_Included();
        }

        [Fact(Skip = "Client-side evaluation not fully supported")]
        public override void Where_subquery_on_navigation_client_eval()
        {
            base.Where_subquery_on_navigation_client_eval();
        }
    }
}
