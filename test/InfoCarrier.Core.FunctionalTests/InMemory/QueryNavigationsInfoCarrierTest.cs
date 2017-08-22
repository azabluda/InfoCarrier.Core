namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;

    public class QueryNavigationsInfoCarrierTest : QueryNavigationsTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public QueryNavigationsInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "Client-side evaluation not fully supported")]
        public override void Where_subquery_on_navigation_client_eval()
        {
            base.Where_subquery_on_navigation_client_eval();
        }
    }
}
