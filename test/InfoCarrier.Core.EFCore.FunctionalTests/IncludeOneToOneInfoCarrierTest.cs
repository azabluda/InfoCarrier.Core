namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class IncludeOneToOneInfoCarrierTest : IncludeOneToOneTestBase, IClassFixture<OneToOneQueryInfoCarrierFixture>
    {
        private readonly OneToOneQueryInfoCarrierFixture fixture;

        public IncludeOneToOneInfoCarrierTest(OneToOneQueryInfoCarrierFixture fixture)
        {
            this.fixture = fixture;
        }

        protected override DbContext CreateContext() => this.fixture.CreateContext();
    }
}
