namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class ComplexNavigationsQueryInfoCarrierTest : ComplexNavigationsQueryTestBase<TestStore, ComplexNavigationsQueryInfoCarrierFixture>
    {
        public ComplexNavigationsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
