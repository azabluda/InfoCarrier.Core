namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class GearsOfWarQueryInfoCarrierTest : GearsOfWarQueryTestBase<TestStore, GearsOfWarQueryInfoCarrierFixture>
    {
        public GearsOfWarQueryInfoCarrierTest(GearsOfWarQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "Revisit after https://github.com/aspnet/EntityFramework/issues/4311 is released")]
        public override void Include_navigation_on_derived_type()
        {
            base.Include_navigation_on_derived_type();
        }
    }
}
