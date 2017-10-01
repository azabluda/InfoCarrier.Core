namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.EntityFrameworkCore.Update;

    public class InfoCarrierDatabase : Database, IInfoCarrierDatabase
    {
        private readonly IInfoCarrierBackend infoCarrierBackend;

        public InfoCarrierDatabase(
            DatabaseDependencies dependencies,
            IDbContextOptions options)
            : base(dependencies)
        {
            this.infoCarrierBackend = options.Extensions.OfType<InfoCarrierOptionsExtension>().First().InfoCarrierBackend;
        }

        public override int SaveChanges(IReadOnlyList<IUpdateEntry> entries)
        {
            ISaveChangesResult result = this.infoCarrierBackend.SaveChanges(new SaveChangesRequest(entries));
            return result.Process(entries);
        }

        public override async Task<int> SaveChangesAsync(
            IReadOnlyList<IUpdateEntry> entries,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ISaveChangesResult result = await this.infoCarrierBackend.SaveChangesAsync(new SaveChangesRequest(entries));
            return result.Process(entries);
        }
    }
}
