// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using System.Threading.Tasks;
    using Client;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Remote.Linq;
    using Server;

    public abstract class TestStoreImplBase : TestStoreBase, IInfoCarrierBackend
    {
        protected abstract DbContextOptions DbContextOptions { get; }

        public override IInfoCarrierBackend InfoCarrierBackend => this;

        public abstract string ServerUrl { get; }

        public override TDbContext CreateContext<TDbContext>(DbContextOptions dbContextOptions)
        {
            return ((TestStoreImplBase<TDbContext>)this).CreateContext(dbContextOptions);
        }

        public abstract TestStoreBase FromShared();

        protected abstract DbContext CreateStoreContextInternal(DbContext clientDbContext);

        public virtual void BeginTransaction()
        {
        }

        public virtual Task BeginTransactionAsync() => Task.CompletedTask;

        public virtual void CommitTransaction()
        {
        }

        public virtual void RollbackTransaction()
        {
        }

        public QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
        {
            using (var helper = new QueryDataHelper(() => this.CreateStoreContextInternal(dbContext), SimulateNetworkTransferJson(request)))
            {
                return SimulateNetworkTransferJson(helper.QueryData());
            }
        }

        public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext)
        {
            using (var helper = new QueryDataHelper(() => this.CreateStoreContextInternal(dbContext), SimulateNetworkTransferJson(request)))
            {
                return SimulateNetworkTransferJson(await helper.QueryDataAsync());
            }
        }

        public SaveChangesResult SaveChanges(SaveChangesRequest request)
        {
            using (SaveChangesHelper helper = this.CreateSaveChangesHelper(request))
            {
                return SimulateNetworkTransferJson(helper.SaveChanges());
            }
        }

        public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request)
        {
            using (SaveChangesHelper helper = this.CreateSaveChangesHelper(request))
            {
                return SimulateNetworkTransferJson(await helper.SaveChangesAsync());
            }
        }

        protected virtual SaveChangesHelper CreateSaveChangesHelper(SaveChangesRequest request)
        {
            request = SimulateNetworkTransferJson(request);
            return new SaveChangesHelper(() => this.CreateStoreContextInternal(null), request);
        }

        private static T SimulateNetworkTransferJson<T>(T value)
        {
            if (value == null)
            {
                return default(T);
            }

            var serializerSettings = new JsonSerializerSettings().ConfigureRemoteLinq();
            var json = JsonConvert.SerializeObject(value, serializerSettings);
            return (T)JsonConvert.DeserializeObject(json, value.GetType(), serializerSettings);
        }

        protected static Action<IServiceCollection> MakeStoreServiceConfigurator(
            Action<ModelBuilder> onModelCreating)
        {
            if (onModelCreating == null)
            {
                return _ => { };
            }

            return services => services.AddSingleton(TestModelSource.GetFactory(onModelCreating));
        }
    }
}
