namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Storage.Internal;
    using Microsoft.Extensions.DependencyInjection;

    public class InfoCarrierInMemoryTestHelper<TDbContext> : InfoCarrierTestHelper<TestStore>
        where TDbContext : DbContext
    {
        private readonly Func<DbContextOptions> buildInMemoryOptions;
        private readonly Func<DbContextOptions, QueryTrackingBehavior, TDbContext> createDbContext;

        public InfoCarrierInMemoryTestHelper(
            Action<ModelBuilder> onModelCreating,
            Func<DbContextOptions, QueryTrackingBehavior, TDbContext> createDbContext,
            Action<WarningsConfigurationBuilder> warningsConfigurationBuilderAction = null)
            : base(onModelCreating)
        {
            this.createDbContext = createDbContext;

            this.buildInMemoryOptions = () =>
            {
                var serviceCollection = new ServiceCollection().AddEntityFrameworkInMemoryDatabase();

                if (onModelCreating != null)
                {
                    serviceCollection.AddSingleton(GetModelSourceFactory<InMemoryModelSource>(onModelCreating, p => new TestInMemoryModelSource(p)));
                }

                return new DbContextOptionsBuilder()
                    .UseInMemoryDatabase()
                    .UseInternalServiceProvider(serviceCollection.BuildServiceProvider())
                    .ConfigureWarnings(warningsConfigurationBuilderAction ?? (_ => { }))
                    .Options;
            };

            this.ResetInMemoryOptions();
        }

        private DbContextOptions InMemoryOptions { get; set; }

        protected override bool IsInMemoryDatabase => true;

        private void ResetInMemoryOptions()
        {
            this.InMemoryOptions = this.buildInMemoryOptions();
        }

        public TestStore CreateTestStore(Action<TDbContext> seedDatabase, bool fullResetInMemoryStore = false)
        {
            using (var context = this.CreateBackendContext())
            {
                seedDatabase(context);
                var storeSource = context.GetService<IInMemoryStoreSource>();
                return new GenericTestStore(() =>
                {
                    storeSource.GetGlobalStore().Clear();
                    if (fullResetInMemoryStore)
                    {
                        this.ResetInMemoryOptions();
                    }
                });
            }
        }

        protected override DbContext CreateBackendContextInternal(
            TestStore testStore,
            QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.CreateBackendContext(queryTrackingBehavior);

        public TDbContext CreateBackendContext(QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.createDbContext(this.InMemoryOptions, queryTrackingBehavior);

        public TDbContext CreateInfoCarrierContext(QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.createDbContext(this.BuildInfoCarrierOptions(null), queryTrackingBehavior);

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
    }
}
