namespace InfoCarrier.Core.FunctionalTests.InMemory
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

        [Fact(Skip = "Not valid for in-memory (from AsyncQueryInMemoryTest)")]
        public override Task ToList_on_nav_in_projection_is_async()
        {
            return base.ToList_on_nav_in_projection_is_async();
        }
    }
}
