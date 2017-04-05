namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Storage.Internal;

    public class InfoCarrierInMemoryTestHelper<TDbContext> : InfoCarrierInMemoryTestHelper
        where TDbContext : DbContext
    {
        private readonly Func<DbContextOptions, QueryTrackingBehavior, TDbContext> createDbContext;

        public InfoCarrierInMemoryTestHelper(
            Action<ModelBuilder> onModelCreating,
            Func<DbContextOptions, QueryTrackingBehavior, TDbContext> createDbContext,
            Action<WarningsConfigurationBuilder> warningsConfigurationBuilderAction = null)
            : base(onModelCreating, warningsConfigurationBuilderAction ?? (_ => { }))
        {
            this.createDbContext = createDbContext;
        }

        public TestStore CreateTestStore(Action<TDbContext> seedDatabase, bool fullResetInMemoryStore = false)
        {
            using (var context = this.CreateInMemoryContext())
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

        protected override DbContext CreateInMemoryContextInternal(QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.CreateInMemoryContext(queryTrackingBehavior);

        public TDbContext CreateInMemoryContext(QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.createDbContext(this.InMemoryOptions, queryTrackingBehavior);

        public TDbContext CreateInfoCarrierContext(QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.createDbContext(this.BuildInfoCarrierOptions(), queryTrackingBehavior);
    }
}
