namespace InfoCarrier.Core.Client.Query.Internal
{
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;

    public class InfoCarrierQueryContextFactory : QueryContextFactory
    {
        //private readonly IInfoCarrierStore _store;

        public InfoCarrierQueryContextFactory(
            IStateManager stateManager,
            IConcurrencyDetector concurrencyDetector,
            //IInfoCarrierStoreSource storeSource,
            IChangeDetector changeDetector,
            IDbContextOptions contextOptions)
            : base(stateManager, concurrencyDetector, changeDetector)
        {
            //_store = storeSource.GetStore(contextOptions);
        }

        public override QueryContext Create()
            => new QueryContext(this.CreateQueryBuffer, this.StateManager, this.ConcurrencyDetector);
    }
}
