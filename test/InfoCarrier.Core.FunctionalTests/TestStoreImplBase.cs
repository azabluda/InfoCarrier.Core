namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Client;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Update;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Remote.Linq;
    using Server;

    public abstract class TestStoreImplBase : TestStoreBase, IInfoCarrierBackend
    {
        protected abstract DbContextOptions DbContextOptions { get; }

        public override IInfoCarrierBackend InfoCarrierBackend => this;

        public string LogFragment => this.DbContextOptions.GetExtension<CoreOptionsExtension>().LogFragment;

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

        public SaveChangesResult SaveChanges(IReadOnlyList<IUpdateEntry> entries)
        {
            using (SaveChangesHelper helper = this.CreateSaveChangesHelper(entries))
            {
                try
                {
                    return SimulateNetworkTransferJson(helper.SaveChanges());
                }
                catch (DbUpdateException e)
                {
                    SimulateNetworkTransferException(e, helper, entries);
                    throw;
                }
            }
        }

        public async Task<SaveChangesResult> SaveChangesAsync(IReadOnlyList<IUpdateEntry> entries)
        {
            using (SaveChangesHelper helper = this.CreateSaveChangesHelper(entries))
            {
                try
                {
                    return SimulateNetworkTransferJson(await helper.SaveChangesAsync());
                }
                catch (DbUpdateException e)
                {
                    SimulateNetworkTransferException(e, helper, entries);
                    throw;
                }
            }
        }

        protected virtual SaveChangesHelper CreateSaveChangesHelper(IEnumerable<IUpdateEntry> entries)
        {
            var request = SimulateNetworkTransferJson(new SaveChangesRequest(entries));
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

        private static void SimulateNetworkTransferException(
            DbUpdateException dbUpdateException,
            SaveChangesHelper helper,
            IReadOnlyList<IUpdateEntry> entries)
        {
            var map = helper.Entries
                .Select((e, i) => new { Index = i, Entry = e })
                .ToDictionary(x => x.Entry, x => x.Index);

            var entityIndexes = dbUpdateException.Entries.Select(re => map[re.GetInfrastructure()]).ToArray();

            IReadOnlyList<IUpdateEntry> failedEntries = entityIndexes.Select(x => entries[x]).ToList();

            if (dbUpdateException is DbUpdateConcurrencyException)
            {
                throw new DbUpdateConcurrencyException(dbUpdateException.Message, failedEntries);
            }

            throw failedEntries.Any()
                ? new DbUpdateException(dbUpdateException.Message, dbUpdateException.InnerException, failedEntries)
                : new DbUpdateException(dbUpdateException.Message, dbUpdateException.InnerException);
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
