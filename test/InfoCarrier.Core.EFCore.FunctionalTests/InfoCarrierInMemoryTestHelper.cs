namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Client;
    using Client.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.Extensions.DependencyInjection;

    public abstract class InfoCarrierInMemoryTestHelper
    {
        private readonly Func<IServiceCollection, DbContextOptions> buildInfoCarrierOptions;

        protected InfoCarrierInMemoryTestHelper(
            Action<ModelBuilder> onModelCreating,
            Action<WarningsConfigurationBuilder> warningsConfigurationBuilderAction)
        {
            this.InMemoryOptions =
                new DbContextOptionsBuilder()
                    .UseInMemoryDatabase()
                    .UseInternalServiceProvider(
                        new ServiceCollection()
                            .AddEntityFrameworkInMemoryDatabase()
                            .AddSingleton(GetModelSourceFactory<InMemoryModelSource>(onModelCreating, p => new TestInMemoryModelSource(p)))
                            .BuildServiceProvider())
                    .ConfigureWarnings(warningsConfigurationBuilderAction)
                    .Options;

            this.buildInfoCarrierOptions = additionalServices =>
                new DbContextOptionsBuilder()
                    .UseInfoCarrierBackend(new TestInfoCarrierBackend(() => this.CreateInMemoryContextInternal(), true))
                    .UseInternalServiceProvider(
                        (additionalServices ?? new ServiceCollection())
                            .AddEntityFrameworkInfoCarrierBackend()
                            .AddSingleton(GetModelSourceFactory<InfoCarrierModelSource>(onModelCreating, p => new TestInfoCarrierModelSource(p)))
                            .BuildServiceProvider())
                    .Options;
        }

        protected DbContextOptions InMemoryOptions { get; }

        public static InfoCarrierInMemoryTestHelper<TDbContext> Create<TDbContext>(
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

        public DbContextOptions BuildInfoCarrierOptions(IServiceCollection additionalServices = null)
            => this.buildInfoCarrierOptions(additionalServices);

        protected abstract DbContext CreateInMemoryContextInternal(QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll);

        private static Func<IServiceProvider, TModelSource> GetModelSourceFactory<TModelSource>(
            Action<ModelBuilder> onModelCreating,
            Func<TestModelSourceParams, TModelSource> creator)
            where TModelSource : ModelSource
            => provider => creator(new TestModelSourceParams(provider, onModelCreating));

        private class TestModelSourceParams
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

        private class TestInMemoryModelSource : InMemoryModelSource
        {
            private readonly TestModelSourceParams testModelSourceParams;

            public TestInMemoryModelSource(TestModelSourceParams p)
                : base(p.SetFinder, p.CoreConventionSetBuilder, p.ModelCustomizer, p.ModelCacheKeyFactory)
            {
                this.testModelSourceParams = p;
            }

            public override IModel GetModel(DbContext context, IConventionSetBuilder conventionSetBuilder, IModelValidator validator)
                => this.testModelSourceParams.GetModel(context, conventionSetBuilder, validator);
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
