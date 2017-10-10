// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.EntityFrameworkCore;
    using RLinq = Remote.Linq.Expressions.Expression;

    [DataContract]
    public class QueryDataRequest
    {
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public QueryDataRequest()
        {
        }

        internal QueryDataRequest(RLinq query, QueryTrackingBehavior trackingBehavior)
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
