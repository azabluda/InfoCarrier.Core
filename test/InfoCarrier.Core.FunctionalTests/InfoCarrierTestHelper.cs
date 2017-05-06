namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public abstract class InfoCarrierTestHelper : IDisposable
    {
        private readonly Action<IServiceCollection> configureInfoCarrierService;
        private readonly Func<TestStoreImplBase> createTestStore;
        private readonly Lazy<TestStoreImplBase> sharedStoreLazy;

        protected InfoCarrierTestHelper(
            Action<IServiceCollection> configureInfoCarrierService,
            Func<TestStoreImplBase> createTestStore,
            bool useSharedStore)
        {
            this.configureInfoCarrierService = configureInfoCarrierService;
            this.createTestStore = createTestStore;

            if (useSharedStore)
            {
                this.sharedStoreLazy = new Lazy<TestStoreImplBase>(this.createTestStore);
            }
        }

        public DbContextOptions BuildInfoCarrierOptions(IInfoCarrierBackend infoCarrierBackend, IServiceCollection additionalServices = null)
        {
            IServiceCollection services = additionalServices ?? new ServiceCollection();
            this.ConfigureInfoCarrierServices(services);
            return new DbContextOptionsBuilder()
                .UseInfoCarrierBackend(infoCarrierBackend)
                .UseInternalServiceProvider(services.BuildServiceProvider())
                .Options;
        }

        public IServiceCollection ConfigureInfoCarrierServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddEntityFrameworkInfoCarrierBackend();
            this.configureInfoCarrierService(serviceCollection);
            return serviceCollection;
        }

        public TestStoreBase CreateTestStore()
            => this.sharedStoreLazy == null
                ? this.createTestStore()
                : this.sharedStoreLazy.Value.FromShared();

        public void Dispose()
        {
            if (this.sharedStoreLazy?.IsValueCreated == true)
            {
                this.sharedStoreLazy.Value.Dispose();
            }
        }
    }
}
