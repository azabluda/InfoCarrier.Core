namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;

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

        protected override DbContext CreateInMemoryContextInternal(QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.CreateInMemoryContext(queryTrackingBehavior);

        public TDbContext CreateInMemoryContext(QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.createDbContext(this.InMemoryOptions, queryTrackingBehavior);

        public TDbContext CreateInfoCarrierContext(QueryTrackingBehavior queryTrackingBehavior = QueryTrackingBehavior.TrackAll)
            => this.createDbContext(this.BuildInfoCarrierOptions(), queryTrackingBehavior);
    }
}
