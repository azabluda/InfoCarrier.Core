namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract(IsReference = true)]
    public class SaveChangesRequest
    {
        [DataMember]
        public List<UpdateEntryDto> DataTransferObjects { get; } = new List<UpdateEntryDto>();
    }
}
