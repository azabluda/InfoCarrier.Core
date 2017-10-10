// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;

    [DataContract]
    [KnownType(typeof(DynamicObject))]
    public class QueryDataResult
    {
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public QueryDataResult()
        {
        }

        internal QueryDataResult(IEnumerable<DynamicObject> result)
        {
            this.MappedResults = result;
        }

        [DataMember]
        public IEnumerable<DynamicObject> MappedResults { get; set; }
    }
}
