namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Client;
    using Client.Infrastructure.Internal;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Update;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Remote.Linq;
    using Remote.Linq.Expressions;
    using Server;

    public abstract class TestStoreImplBase : TestStoreBase, IInfoCarrierBackend
    {
        protected abstract DbContextOptions DbContextOptions { get; }

        public override IInfoCarrierBackend InfoCarrierBackend => this;

        public override TDbContext CreateContext<TDbContext>(DbContextOptions dbContextOptions)
        {
            return ((TestStoreImplBase<TDbContext>)this).CreateContext(dbContextOptions);
        }

        public abstract TestStoreBase FromShared();

        protected abstract DbContext CreateContextInternal();

        public virtual void BeginTransaction()
        {
        }

        public virtual Task BeginTransactionAsync()
        {
            return Task.FromResult(true);
        }

        public virtual void CommitTransaction()
        {
        }

        public virtual void RollbackTransaction()
        {
        }

        public IEnumerable<DynamicObject> QueryData(Expression rlinq)
        {
            using (var helper = new QueryDataHelper(this.CreateContextInternal, SimulateNetworkTransferJson(rlinq)))
            {
                return SimulateNetworkTransferJson(helper.QueryData());
            }
        }

        public async Task<IEnumerable<DynamicObject>> QueryDataAsync(Expression rlinq)
        {
            using (var helper = new QueryDataHelper(this.CreateContextInternal, SimulateNetworkTransferJson(rlinq)))
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
            return new SaveChangesHelper(this.CreateContextInternal, request);
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

        protected static Action<IServiceCollection> MakeStoreServiceConfigurator<TModelSource>(
            Action<ModelBuilder> onModelCreating,
            Func<TestModelSourceParams, TModelSource> creator)
            where TModelSource : ModelSource
        {
            if (onModelCreating == null)
            {
                return _ => { };
            }

            return services => services.AddSingleton(provider => creator(new TestModelSourceParams(provider, onModelCreating)));
        }

        protected class TestModelSourceParams
        {
            public TestModelSourceParams(IServiceProvider provider, Action<ModelBuilder> onModelCreating)
            {
                this.SetFinder = provider.GetRequiredService<IDbSetFinder>();
                this.CoreConventionSetBuilder = provider.GetRequiredService<ICoreConventionSetBuilder>();
                this.ModelCustomizer = new ModelCustomizer();
                this.ModelCacheKeyFactory = new ModelCacheKeyFactory();

                var testModelSource = new TestModelSource(
                    onModelCreating,
                    this.SetFinder,
                    this.CoreConventionSetBuilder,
                    new ModelCustomizer(),
                    new ModelCacheKeyFactory());

                this.GetModel = (context, conventionSetBuilder, modelValidator)
                    => testModelSource.GetModel(context, conventionSetBuilder, modelValidator);
            }

            public IDbSetFinder SetFinder { get; }

            public ICoreConventionSetBuilder CoreConventionSetBuilder { get; }

            public IModelCustomizer ModelCustomizer { get; }

            public IModelCacheKeyFactory ModelCacheKeyFactory { get; }

            public Func<DbContext, IConventionSetBuilder, IModelValidator, IModel> GetModel { get; }
        }

        protected class TestInfoCarrierModelSource : InfoCarrierModelSource
        {
            private readonly TestModelSourceParams testModelSourceParams;

            public TestInfoCarrierModelSource(TestModelSourceParams p)
                : base(p.SetFinder, p.CoreConventionSetBuilder, p.ModelCustomizer, p.ModelCacheKeyFactory)
            {
                this.testModelSourceParams = p;
            }

            public override IModel GetModel(DbContext context, IConventionSetBuilder conventionSetBuilder, IModelValidator validator)
                => this.testModelSourceParams.GetModel(context, conventionSetBuilder, validator);
        }
    }
}
