namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class UpdatesInfoCarrierTest : UpdatesTestBase<UpdatesInfoCarrierFixture, TestStore>
    {
        public UpdatesInfoCarrierTest(UpdatesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        protected override string UpdateConcurrencyMessage
            => InMemoryStrings.UpdateConcurrencyException;
    }
}