namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;

    public class OwnedQueryInfoCarrierTest : OwnedQueryTestBase, IClassFixture<OwnedQueryInfoCarrierFixture>
    {
        private readonly OwnedQueryInfoCarrierFixture fixture;

        public OwnedQueryInfoCarrierTest(OwnedQueryInfoCarrierFixture fixture)
        {
            this.fixture = fixture;
        }

        protected override DbContext CreateContext() => this.fixture.CreateContext();
    }
}
