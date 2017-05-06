namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.Internal;

    public class InfoCarrierQueryContext : QueryContext
    {
        public InfoCarrierQueryContext(
            Func<IQueryBuffer> createQueryBuffer,
            IInfoCarrierBackend infoCarrierBackend,
            LazyRef<IStateManager> stateManager,
            IConcurrencyDetector concurrencyDetector)
            : base(
                createQueryBuffer,
                stateManager,
                concurrencyDetector)
        {
            this.InfoCarrierBackend = infoCarrierBackend;
        }

        public IInfoCarrierBackend InfoCarrierBackend { get; }
    }
}