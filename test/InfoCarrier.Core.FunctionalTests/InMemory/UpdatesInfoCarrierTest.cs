namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Internal;

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