namespace InfoCarrier.Core.Client.Query.Internal
{
    using System.Linq;
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;

    public class InfoCarrierQueryContextFactory : QueryContextFactory
    {
        private readonly IInfoCarrierBackend infoCarrierBackend;

        public InfoCarrierQueryContextFactory(
            ICurrentDbContext currentContext,
            IConcurrencyDetector concurrencyDetector,
            IDbContextOptions contextOptions)
            : base(currentContext, concurrencyDetector)
        {
            this.infoCarrierBackend = contextOptions.Extensions.OfType<InfoCarrierOptionsExtension>().First().InfoCarrierBackend;
        }

        public override QueryContext Create()
            => new InfoCarrierQueryContext(this.CreateQueryBuffer, this.infoCarrierBackend, this.StateManager, this.ConcurrencyDetector);
    }
}
