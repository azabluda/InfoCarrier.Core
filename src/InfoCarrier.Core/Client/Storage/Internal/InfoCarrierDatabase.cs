namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
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
            SaveChangesResult result = this.infoCarrierBackend.SaveChanges(new SaveChangesRequest(entries));
            return ProcessResults(entries, result);
        }

        public override async Task<int> SaveChangesAsync(
            IReadOnlyList<IUpdateEntry> entries,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SaveChangesResult result = await this.infoCarrierBackend.SaveChangesAsync(new SaveChangesRequest(entries));
            return ProcessResults(entries, result);
        }

        private static int ProcessResults(IReadOnlyList<IUpdateEntry> entries, SaveChangesResult result)
        {
            if (result.ExceptionInfo != null)
            {
                IReadOnlyList<IUpdateEntry> failedEntries = result.ExceptionInfo.FailedEntityIndexes.Select(x => entries[x]).ToList();

                if (result.ExceptionInfo.TypeName == typeof(DbUpdateConcurrencyException).FullName)
                {
                    throw new DbUpdateConcurrencyException(result.ExceptionInfo.Message, failedEntries);
                }

                if (result.ExceptionInfo.TypeName == typeof(DbUpdateException).FullName)
                {
                    throw failedEntries.Any()
                        ? new DbUpdateException(result.ExceptionInfo.Message, failedEntries)
                        : new DbUpdateException(result.ExceptionInfo.Message, (Exception)null);
                }

                throw new InvalidOperationException($@"Unknown server-side exception {result.ExceptionInfo.TypeName} : {result.ExceptionInfo.Message}");
            }

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
