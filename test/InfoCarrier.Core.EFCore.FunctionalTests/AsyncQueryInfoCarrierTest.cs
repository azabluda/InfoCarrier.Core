namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class AsyncQueryInfoCarrierTest : AsyncQueryTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public AsyncQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "https://github.com/6bee/Remote.Linq/issues/4")]
        public override Task SelectMany_mixed()
        {
            return base.SelectMany_mixed();
        }
    }
}
