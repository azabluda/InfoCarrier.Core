namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.UpdatesModel;

    public class UpdatesInfoCarrierFixture : UpdatesFixtureBase<TestStore>
    {
        private readonly InfoCarrierInMemoryTestHelper<UpdatesContext> helper;

        public UpdatesInfoCarrierFixture()
        {
            this.helper = InfoCarrierTestHelper.CreateInMemory(
                null,
                (opt, _) => new UpdatesContext(opt),
                w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        }

        public override TestStore CreateTestStore()
            => this.helper.CreateTestStore(UpdatesModelInitializer.Seed);

        public override UpdatesContext CreateContext(TestStore testStore)
            => this.helper.CreateInfoCarrierContext();
    }
}