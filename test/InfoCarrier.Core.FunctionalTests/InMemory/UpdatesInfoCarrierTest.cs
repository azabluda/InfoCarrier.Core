namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class UpdatesInfoCarrierTest : UpdatesTestBase<UpdatesInfoCarrierFixture, TestStoreBase>
    {
        public UpdatesInfoCarrierTest(UpdatesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        protected override string UpdateConcurrencyMessage
            => InMemoryStrings.UpdateConcurrencyException;
    }
}