namespace InfoCarrier.Core.Common
{
    using System.Runtime.Serialization;
    using Microsoft.EntityFrameworkCore;
    using RLinq = Remote.Linq.Expressions.Expression;

    [DataContract]
    public class QueryDataRequest
    {
        public QueryDataRequest()
        {
        }

        public QueryDataRequest(RLinq query, QueryTrackingBehavior trackingBehavior)
        {
            this.Query = query;
            this.TrackingBehavior = trackingBehavior;
        }

        [DataMember]
        public RLinq Query { get; set; }

        [DataMember]
        public QueryTrackingBehavior TrackingBehavior { get; set; }
    }
}
