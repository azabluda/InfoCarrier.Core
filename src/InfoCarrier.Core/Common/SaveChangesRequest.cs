namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore.Update;

    [DataContract]
    public class SaveChangesRequest
    {
        private IDynamicObjectMapper mapper;

        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public SaveChangesRequest()
        {
        }

        internal SaveChangesRequest(IEnumerable<IUpdateEntry> entries)
        {
            this.DataTransferObjects.AddRange(entries.Select(e => new UpdateEntryDto(e, this.Mapper)));
        }

        [IgnoreDataMember]
        internal IDynamicObjectMapper Mapper
            => this.mapper ?? (this.mapper = new DynamicObjectMapper(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true }));

        [DataMember]
        public List<UpdateEntryDto> DataTransferObjects { get; set; } = new List<UpdateEntryDto>();
    }
}
