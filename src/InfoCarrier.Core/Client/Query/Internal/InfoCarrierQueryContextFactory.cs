namespace InfoCarrier.Core.Client.Query.Internal
{
    using System.Linq;
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query;

    public class InfoCarrierQueryContextFactory : QueryContextFactory
    {
        private readonly IInfoCarrierBackend infoCarrierBackend;

        public InfoCarrierQueryContextFactory(
            QueryContextDependencies dependencies,
            IDbContextOptions contextOptions)
            : base(dependencies)
        {
            this.infoCarrierBackend = contextOptions.Extensions.OfType<InfoCarrierOptionsExtension>().First().InfoCarrierBackend;
        }

        public override QueryContext Create()
            => new InfoCarrierQueryContext(this.Dependencies, this.CreateQueryBuffer, this.infoCarrierBackend);
    }
}
