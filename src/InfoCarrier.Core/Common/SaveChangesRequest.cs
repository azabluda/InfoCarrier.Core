namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore.Update;

    [DataContract]
    public class SaveChangesRequest
    {
        public SaveChangesRequest()
        {
        }

        public SaveChangesRequest(IEnumerable<IUpdateEntry> entries)
        {
            this.DataTransferObjects.AddRange(entries.Select(e => new UpdateEntryDto(e, this.Mapper)));
        }

        [IgnoreDataMember]
        internal IDynamicObjectMapper Mapper { get; }
            = new DynamicObjectMapper(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true });

        [DataMember]
        public List<UpdateEntryDto> DataTransferObjects { get; } = new List<UpdateEntryDto>();
    }
}
