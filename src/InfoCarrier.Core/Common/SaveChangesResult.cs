namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Update;

    [DataContract]
    public class SaveChangesResult : SaveChangesRequest
    {
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public SaveChangesResult()
        {
        }

        internal SaveChangesResult(int countPersisted, IEnumerable<IUpdateEntry> entries)
            : base(entries)
        {
            this.CountPersisted = countPersisted;
        }

        internal SaveChangesResult(DbUpdateException exception, IEnumerable<IUpdateEntry> entries)
            : base(null)
        {
            this.ExceptionInfo = new DbExceptionInfo(exception, entries);
        }

        [DataMember]
        public int CountPersisted { get; set; }

        [DataMember]
        public DbExceptionInfo ExceptionInfo { get; set; }

        [DataContract]
        public class DbExceptionInfo
        {
            [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
            public DbExceptionInfo()
            {
            }

            internal DbExceptionInfo(DbUpdateException exception, IEnumerable<IUpdateEntry> entries)
            {
                this.TypeName = exception.GetType().FullName;
                this.Message = exception.Message;

                var map = entries
                    .Select((e, i) => new { Index = i, Entry = e })
                    .ToDictionary(x => x.Entry, x => x.Index);
                this.FailedEntityIndexes = exception.Entries.Select(re => map[re.GetInfrastructure()]).ToArray();
            }

            [DataMember]
            public string TypeName { get; set; }

            [DataMember]
            public string Message { get; set; }

            [DataMember]
            public int[] FailedEntityIndexes { get; set; }
        }
    }
}
