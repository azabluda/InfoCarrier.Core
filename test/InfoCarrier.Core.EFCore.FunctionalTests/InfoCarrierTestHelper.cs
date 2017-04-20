namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Client;
    using Client.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.Extensions.DependencyInjection;

    public static class InfoCarrierTestHelper
    {
        public static InfoCarrierInMemoryTestHelper<TDbContext> CreateInMemory<TDbContext>(
            Action<ModelBuilder> onModelCreating,
            Func<DbContextOptions, QueryTrackingBehavior, TDbContext> createDbContext,
            Action<WarningsConfigurationBuilder> warningsConfigurationBuilderAction = null)
            where TDbContext : DbContext
        {
            return new InfoCarrierInMemoryTestHelper<TDbContext>(
                onModelCreating,
                createDbContext,
                warningsConfigurationBuilderAction);
        }

        public static InfoCarrierSqlServerTestHelper<TDbContext> CreateSqlServer<TDbContext>(
            string databaseName,
            Action<ModelBuilder> onModelCreating,
            Func<DbContextOptions, QueryTrackingBehavior, TDbContext> createDbContext)
            where TDbContext : DbContext
        {
            return new InfoCarrierSqlServerTestHelper<TDbContext>(
                databaseName,
                onModelCreating,
                createDbContext);
        }
    }

    public abstract class InfoCarrierTestHelper<TTestStore> : IInfoCarrierTestHelper<TTestStore>
        where TTestStore : TestStore
    {
        private readonly Func<TTestStore, IServiceCollection, DbContextOptions> buildInfoCarrierOptions;
        private readonly Action<IServiceCollection> configureInfoCarrierServices;

        protected InfoCarrierTestHelper(
            Action<ModelBuilder> onModelCreating)
        {
            this.configureInfoCarrierServices = services =>
            {
                services.AddEntityFrameworkInfoCarrierBackend();
                if (onModelCreating != null)
                {
                    services.AddSingleton(GetModelSourceFactory<InfoCarrierModelSource>(onModelCreating, p => new TestInfoCarrierModelSource(p)));
                }
            };

            this.buildInfoCarrierOptions = (testStore, additionalServices) =>
                new DbContextOptionsBuilder()
                    .UseInfoCarrierBackend(new TestInfoCarrierBackend(() => this.CreateBackendContextInternal(testStore), this.IsInMemoryDatabase, (testStore as RelationalTestStore)?.Connection))
                    .UseInternalServiceProvider(
                        this.ConfigureInfoCarrierServices(additionalServices ?? new ServiceCollection())
                            .BuildServiceProvider())
                    .Options;
        }

        protected virtual bool IsInMemoryDatabase => false;

        public IServiceCollection ConfigureInfoCarrierServices(IServiceCollection services)
        {
            this.configureInfoCarrierServices(services);
            return services;
        }

        public DbContextOptions BuildInfoCarrierOptions(
            TTestStore testStore,
            IServiceCollection additionalServices = null)
            => this.buildInfoCarrierOptions(testStore, additionalServices);

        protected abstract DbContext CreateBackendContextInternal(
            TTestStore testStore,
            QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll);

        protected static Func<IServiceProvider, TModelSource> GetModelSourceFactory<TModelSource>(
            Action<ModelBuilder> onModelCreating,
            Func<TestModelSourceParams, TModelSource> creator)
            where TModelSource : ModelSource
            => provider => creator(new TestModelSourceParams(provider, onModelCreating));

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

        private class TestInfoCarrierModelSource : InfoCarrierModelSource
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
