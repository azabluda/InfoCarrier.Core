namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore;
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
            SaveChangesResult result = this.infoCarrierBackend.SaveChanges(new SaveChangesRequest(entries), entries);
            return MergeResults(entries, result);
        }

        public override async Task<int> SaveChangesAsync(
            IReadOnlyList<IUpdateEntry> entries,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SaveChangesResult result = await this.infoCarrierBackend.SaveChangesAsync(new SaveChangesRequest(entries), entries);
            return MergeResults(entries, result);
        }

        private static int MergeResults(IReadOnlyList<IUpdateEntry> entries, SaveChangesResult result)
        {
            // Merge the results / update properties modified during SaveChanges on the server-side
            foreach (var merge in entries.Zip(result.DataTransferObjects, (e, d) => new { Entry = e, Dto = d }))
            {
                foreach (var p in merge.Dto.JoinScalarProperties(merge.Entry.ToEntityEntry(), result.Mapper))
                {
                    // Can not (and need not) merge non-temporary PK values
                    if (p.EfProperty.Metadata.IsKey() && !p.EfProperty.IsTemporary)
                    {
                        continue;
                    }

                    p.EfProperty.CurrentValue = p.CurrentValue;
                }
            }

            return result.CountPersisted;
        }
    }
}
