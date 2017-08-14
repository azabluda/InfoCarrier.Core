namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;

    [DataContract]
    public class QueryDataResult
    {
        public QueryDataResult()
        {
        }

        public QueryDataResult(IEnumerable<DynamicObject> result)
        {
            this.MappedResults = result;
        }

        [DataMember]
        public IEnumerable<DynamicObject> MappedResults { get; set; }
    }
}
