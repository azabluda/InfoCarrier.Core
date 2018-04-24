// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;

    public abstract class InfoCarrierBackendTestStore : TestStore
    {
        private readonly SharedTestStoreProperties testStoreProperties;

        protected InfoCarrierBackendTestStore(
            string name,
            bool shared,
            SharedTestStoreProperties testStoreProperties)
            : base(name, shared)
        {
            this.testStoreProperties = testStoreProperties;
            this.ServiceProvider = this.AddServices(new ServiceCollection())
                .AddSingleton(TestModelSource.GetFactory(testStoreProperties.OnModelCreating))
                .AddDbContext(
                    this.testStoreProperties.ContextType,
                    (s, b) => this.AddProviderOptions(b),
                    ServiceLifetime.Transient,
                    ServiceLifetime.Singleton)
                .BuildServiceProvider();
        }

        public virtual DbContext CreateDbContext()
            => (DbContext)this.ServiceProvider.GetRequiredService(this.testStoreProperties.ContextType);

        protected abstract IServiceCollection AddServices(IServiceCollection serviceCollection);

        public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
            => this.testStoreProperties.OnAddOptions(builder.UseInternalServiceProvider(this.ServiceProvider));

        public abstract void BeginTransaction();

        public abstract Task BeginTransactionAsync(CancellationToken cancellationToken);

        public abstract void CommitTransaction();

        public abstract void RollbackTransaction();
    }
}
