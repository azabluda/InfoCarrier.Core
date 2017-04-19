namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class ComplexNavigationsQueryInfoCarrierTest : ComplexNavigationsQueryTestBase<TestStore, ComplexNavigationsQueryInfoCarrierFixture>
    {
        public ComplexNavigationsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "https://github.com/aspnet/EntityFramework/issues/7559")]
        public override void Optional_navigation_projected_into_DTO()
        {
            base.Optional_navigation_projected_into_DTO();
        }

        [Fact(Skip = "https://github.com/aspnet/EntityFramework/issues/7559")]
        public override void Key_equality_using_property_method_nested()
        {
            base.Key_equality_using_property_method_nested();
        }
    }
}
