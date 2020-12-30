// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Serialization;
    using Microsoft.EntityFrameworkCore;
    using RLinq = Remote.Linq.Expressions.Expression;

    /// <summary>
    ///     A serializable object containing the Remote.Linq query expression for its execution on the server-side.
    /// </summary>
    [DataContract]
    public class QueryDataRequest
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="QueryDataRequest"/> class.
        /// </summary>
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public QueryDataRequest()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="QueryDataRequest"/> class.
        /// </summary>
        /// <param name="query">The Remote.Linq query expression.</param>
        /// <param name="trackingBehavior">The <see cref="QueryTrackingBehavior" /> of the client-side <see cref="DbContext" />.</param>
        internal QueryDataRequest(RLinq query, QueryTrackingBehavior trackingBehavior)
        {
            this.Query = query;
            this.TrackingBehavior = trackingBehavior;
        }

        /// <summary>
        ///     Gets or sets the Remote.Linq query expression.
        /// </summary>
        [DataMember]
        public RLinq Query { get; set; }

        /// <summary>
        ///     Gets or sets the <see cref="QueryTrackingBehavior" /> which the server-side <see cref="DbContext" /> must match.
        /// </summary>
        [DataMember]
        public QueryTrackingBehavior TrackingBehavior { get; set; }

        /// <summary>
        ///     Convert this object into a string representation.
        /// </summary>
        /// <returns>
        ///     A string that represents this object.
        /// </returns>
        [ExcludeFromCodeCoverage]
        public override string ToString()
            => $"{this.Query} {this.TrackingBehavior}";
    }
}
