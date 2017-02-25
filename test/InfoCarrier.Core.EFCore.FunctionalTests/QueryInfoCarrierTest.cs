namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class QueryInfoCarrierTest : QueryTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public QueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip="Too slow")]
        public override void OrderBy_correlated_subquery_lol2()
        {
            base.OrderBy_correlated_subquery_lol2();
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

        [Fact(Skip = "https://github.com/6bee/Remote.Linq/issues/7")]
        public override void Where_subquery_expression_same_parametername()
        {
            base.Where_subquery_expression_same_parametername();
        }
    }
}
