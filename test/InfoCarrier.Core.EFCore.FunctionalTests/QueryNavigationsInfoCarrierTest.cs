namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class QueryNavigationsInfoCarrierTest : QueryNavigationsTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public QueryNavigationsInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip= "https://github.com/aspnet/EntityFramework/issues/7559")]
        public override void Select_Where_Navigation_Included()
        {
            base.Select_Where_Navigation_Included();
        }

        [Fact(Skip = "https://github.com/6bee/Remote.Linq/issues/5")]
        public override void Select_Where_Navigation_Contains()
        {
            base.Select_Where_Navigation_Contains();
        }
    }
}
