namespace InfoCarrier.Core.Client.Query.Internal
{
    using System.Linq;
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;

    public class InfoCarrierQueryContextFactory : QueryContextFactory
    {
        private readonly ServerContext serverContext;

        public InfoCarrierQueryContextFactory(
            IStateManager stateManager,
            IConcurrencyDetector concurrencyDetector,
            //IInfoCarrierStoreSource storeSource,
            IChangeDetector changeDetector,
            IDbContextOptions contextOptions)
            : base(stateManager, concurrencyDetector, changeDetector)
        {
            this.serverContext = contextOptions.Extensions.OfType<InfoCarrierOptionsExtension>().First().ServerContext;
        }

        public override QueryContext Create()
            => new InfoCarrierQueryContext(this.CreateQueryBuffer, this.serverContext, this.StateManager, this.ConcurrencyDetector);
    }
}
