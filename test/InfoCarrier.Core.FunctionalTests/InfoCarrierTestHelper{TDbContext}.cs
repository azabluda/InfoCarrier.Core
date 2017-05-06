namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public class InfoCarrierTestHelper<TDbContext> : InfoCarrierTestHelper
        where TDbContext : DbContext
    {
        public InfoCarrierTestHelper(
            Action<IServiceCollection> configureInfoCarrierService,
            Func<TestStoreImplBase<TDbContext>> createTestStore,
            bool useSharedStore)
            : base(configureInfoCarrierService, createTestStore, useSharedStore)
        {
        }

        public TDbContext CreateInfoCarrierContext(TestStoreBase testStore)
            => testStore.CreateContext<TDbContext>(this.BuildInfoCarrierOptions(testStore.InfoCarrierBackend));
    }
}
