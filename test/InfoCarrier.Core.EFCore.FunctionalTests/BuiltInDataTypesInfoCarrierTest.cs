namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class BuiltInDataTypesInfoCarrierTest : BuiltInDataTypesTestBase<BuiltInDataTypesInfoCarrierFixture>
    {
        public BuiltInDataTypesInfoCarrierTest(BuiltInDataTypesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public virtual void Can_perform_query_with_ansi_strings()
        {
            this.Can_perform_query_with_ansi_strings(supportsAnsi: false);
        }
    }
}
