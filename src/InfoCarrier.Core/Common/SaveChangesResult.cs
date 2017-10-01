namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Update;

    public static class SaveChangesResult
    {
        [DataContract]
        public class Success : SaveChangesRequest, ISaveChangesResult
        {
            [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
            public Success()
            {
            }

            internal Success(int countPersisted, IEnumerable<IUpdateEntry> entries)
                : base(entries)
            {
                this.CountPersisted = countPersisted;
            }

            [DataMember]
            public int CountPersisted { get; set; }

            int ISaveChangesResult.Process(IReadOnlyList<IUpdateEntry> entries)
            {
                // Merge the results / update properties modified during SaveChanges on the server-side
                foreach (var merge in entries.Zip(this.DataTransferObjects, (e, d) => new { Entry = e, Dto = d }))
                {
                    foreach (var p in merge.Dto.JoinScalarProperties(merge.Entry.ToEntityEntry(), this.Mapper))
                    {
                        // Can not (and need not) merge non-temporary PK values
                        if (p.EfProperty.Metadata.IsKey() && !p.EfProperty.IsTemporary)
                        {
                            continue;
                        }

                        p.EfProperty.CurrentValue = p.CurrentValue;
                    }
                }

                return this.CountPersisted;
            }
        }

        [DataContract]
        public class Error : ISaveChangesResult
        {
            [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
            public Error()
            {
            }

            internal Error(DbUpdateException exception, IEnumerable<IUpdateEntry> entries)
            {
                this.IsConcurrencyException = exception is DbUpdateConcurrencyException;
                this.Message = exception.Message;

                var map = entries
                    .Select((e, i) => new { Index = i, Entry = e })
                    .ToDictionary(x => x.Entry, x => x.Index);
                this.FailedEntityIndexes = exception.Entries.Select(re => map[re.GetInfrastructure()]).ToArray();
            }

            [DataMember]
            public bool IsConcurrencyException { get; set; }

            [DataMember]
            public string Message { get; set; }

            [DataMember]
            public int[] FailedEntityIndexes { get; set; }

            int ISaveChangesResult.Process(IReadOnlyList<IUpdateEntry> entries)
            {
                IReadOnlyList<IUpdateEntry> failedEntries = this.FailedEntityIndexes.Select(x => entries[x]).ToList();

                if (this.IsConcurrencyException)
                {
                    throw new DbUpdateConcurrencyException(this.Message, failedEntries);
                }

                throw failedEntries.Any()
                    ? new DbUpdateException(this.Message, failedEntries)
                    : new DbUpdateException(this.Message, (Exception)null);
            }
        }
    }
}
