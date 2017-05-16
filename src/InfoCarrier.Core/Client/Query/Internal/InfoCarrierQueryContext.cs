namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.Internal;

    public class InfoCarrierQueryContext : QueryContext
    {
        public InfoCarrierQueryContext(
            QueryContextDependencies dependencies,
            Func<IQueryBuffer> queryBufferFactory,
            IInfoCarrierBackend infoCarrierBackend)
            : base(dependencies, queryBufferFactory)
        {
            this.InfoCarrierBackend = infoCarrierBackend;
        }

        public IInfoCarrierBackend InfoCarrierBackend { get; }
    }
}