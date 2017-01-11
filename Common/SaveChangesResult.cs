namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract(IsReference = true)]
    public class SaveChangesResult
    {
        [DataMember]
        public int CountPersisted { get; set; }

        [DataMember]
        public List<DataTransferObject> DataTransferObjects { get; } = new List<DataTransferObject>();
    }
}
