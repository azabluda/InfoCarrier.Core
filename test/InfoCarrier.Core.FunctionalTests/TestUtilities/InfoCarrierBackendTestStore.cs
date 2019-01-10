// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Common.ValueMapping;
    using InfoCarrier.Core.Server;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Remote.Linq;

    public abstract class InfoCarrierBackendTestStore : TestStore, IInfoCarrierBackend
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
                .AddInfoCarrierServer()
                .AddSingleton<IInfoCarrierValueMapper, InfoCarrierNetTopologySuiteValueMapper>()
                .AddSingleton(TestModelSource.GetFactory(this.testStoreProperties.OnModelCreating))
                .AddDbContext(
                    this.testStoreProperties.ContextType,
                    (s, b) => this.AddProviderOptions(b),
                    ServiceLifetime.Transient,
                    ServiceLifetime.Singleton)
                .BuildServiceProvider();
        }

        public string ServerUrl => this.Name;

        public virtual DbContext CreateDbContext()
            => (DbContext)this.ServiceProvider.GetRequiredService(this.testStoreProperties.ContextType);

        public virtual IInfoCarrierServer CreateInfoCarrierServer()
            => this.ServiceProvider.GetRequiredService<IInfoCarrierServer>();

        protected abstract IServiceCollection AddServices(IServiceCollection serviceCollection);

        public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
            => this.testStoreProperties.OnAddOptions(builder.UseInternalServiceProvider(this.ServiceProvider));

        public QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
            => SimulateNetworkTransferJson(
                this.CreateInfoCarrierServer().QueryData(
                    this.CreateDbContextWithParameters(dbContext),
                    SimulateNetworkTransferJson(request)));

        public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext, CancellationToken cancellationToken)
            => SimulateNetworkTransferJson(
                await this.CreateInfoCarrierServer().QueryDataAsync(
                    this.CreateDbContextWithParameters(dbContext),
                    SimulateNetworkTransferJson(request),
                    cancellationToken));

        public SaveChangesResult SaveChanges(SaveChangesRequest request)
            => SimulateNetworkTransferJson(
                this.CreateInfoCarrierServer().SaveChanges(
                    this.CreateDbContext,
                    SimulateNetworkTransferJson(request)));

        public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken)
            => SimulateNetworkTransferJson(
                await this.CreateInfoCarrierServer().SaveChangesAsync(
                    this.CreateDbContext,
                    SimulateNetworkTransferJson(request),
                    cancellationToken));

        public abstract void BeginTransaction();

        public abstract Task BeginTransactionAsync(CancellationToken cancellationToken);

        public abstract void CommitTransaction();

        public abstract void RollbackTransaction();

        private static T SimulateNetworkTransferJson<T>(T value)
        {
            if (value == null)
            {
                return default;
            }

            var serializerSettings = new JsonSerializerSettings().ConfigureRemoteLinq();
            var json = JsonConvert.SerializeObject(value, serializerSettings);
            return (T)JsonConvert.DeserializeObject(json, value.GetType(), serializerSettings);
        }

        private Func<DbContext> CreateDbContextWithParameters(DbContext clientDbContext) =>
            () =>
            {
                var backendDbContext = this.CreateDbContext();
                this.testStoreProperties.CopyDbContextParameters?.Invoke(clientDbContext, backendDbContext);
                return backendDbContext;
            };
    }
}
