namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Microsoft.EntityFrameworkCore.Update;

    [DataContract]
    public class SaveChangesResult : SaveChangesRequest
    {
        public SaveChangesResult()
        {
        }

        public SaveChangesResult(int countPersisted, IEnumerable<IUpdateEntry> entries)
            : base(entries)
        {
            this.CountPersisted = countPersisted;
        }

        [DataMember]
        public int CountPersisted { get; set; }
    }
}
