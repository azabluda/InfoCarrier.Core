namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
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

        [DataMember]
        public int CountPersisted { get; set; }
    }
}
