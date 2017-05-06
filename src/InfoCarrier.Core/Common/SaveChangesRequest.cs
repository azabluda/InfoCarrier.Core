namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Microsoft.EntityFrameworkCore.Update;

    [DataContract]
    public class SaveChangesRequest
    {
        public SaveChangesRequest()
        {
        }

        public SaveChangesRequest(IEnumerable<IUpdateEntry> entries)
        {
            this.DataTransferObjects.AddRange(entries.Select(e => new UpdateEntryDto(e)));
        }

        [DataMember]
        public List<UpdateEntryDto> DataTransferObjects { get; } = new List<UpdateEntryDto>();
    }
}
