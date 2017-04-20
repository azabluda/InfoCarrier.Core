namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.Extensions.DependencyInjection;

    public class InfoCarrierSqlServerTestHelper<TDbContext> : InfoCarrierTestHelper<SqlServerTestStore>,
        IDisposable
        where TDbContext : DbContext
    {
        private readonly string databaseName;
        private readonly Func<DbContextOptions, QueryTrackingBehavior, TDbContext> createDbContext;
        private readonly Lazy<SqlServerTestStore> sharedStore;

        public InfoCarrierSqlServerTestHelper(
            string databaseName,
            Action<ModelBuilder> onModelCreating,
            Func<DbContextOptions, QueryTrackingBehavior, TDbContext> createDbContext)
            : base(onModelCreating)
        {
            this.databaseName = databaseName;
            this.createDbContext = createDbContext;

            var serviceCollection = new ServiceCollection().AddEntityFrameworkSqlServer();
            if (onModelCreating != null)
            {
                serviceCollection.AddSingleton(GetModelSourceFactory<SqlServerModelSource>(onModelCreating, p => new TestSqlServerModelSource(p)));
            }

            this.sharedStore = new Lazy<SqlServerTestStore>(() => new SqlServerTestStore(databaseName, serviceCollection.BuildServiceProvider(), this));
        }

        protected override bool IsInMemoryDatabase => false;

        public SqlServerTestStore CreateTestStore(Action<TDbContext> seedDatabase)
        {
            if (this.sharedStore.IsValueCreated)
            {
                return this.sharedStore.Value;
            }

            using (var context = this.CreateBackendContext(this.sharedStore.Value))
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
                seedDatabase(context);
            }

            return this.sharedStore.Value;
        }

        protected override DbContext CreateBackendContextInternal(
            SqlServerTestStore testStore,
            QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.CreateBackendContext(testStore, queryTrackingBehavior);

        public TDbContext CreateBackendContext(
            SqlServerTestStore testStore,
            QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.createDbContext(testStore.SqlServerOptions, queryTrackingBehavior);

        public TDbContext CreateInfoCarrierContext(
            SqlServerTestStore testStore,
            QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.createDbContext(testStore.InfoCarrierOptions, queryTrackingBehavior);

        public void Dispose()
        {
            if (this.sharedStore.IsValueCreated)
            {
                this.sharedStore.Value.Dispose();
            }
        }

        private class TestSqlServerModelSource : SqlServerModelSource
        {
            private readonly TestModelSourceParams testModelSourceParams;

            public TestSqlServerModelSource(TestModelSourceParams p)
                : base(p.SetFinder, p.CoreConventionSetBuilder, p.ModelCustomizer, p.ModelCacheKeyFactory)
            {
                this.testModelSourceParams = p;
            }

            public override IModel GetModel(DbContext context, IConventionSetBuilder conventionSetBuilder, IModelValidator validator)
                => this.testModelSourceParams.GetModel(context, conventionSetBuilder, validator);
        }
    }
}
