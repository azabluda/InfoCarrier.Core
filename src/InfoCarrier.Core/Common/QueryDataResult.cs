// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;

    /// <summary>
    ///     A serializable object containing the query execution results mapped to <see cref="DynamicObject" />'s.
    /// </summary>
    [DataContract]
    [KnownType(typeof(DynamicObject))]
    public class QueryDataResult
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="QueryDataResult"/> class.
        /// </summary>
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public QueryDataResult()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="QueryDataResult"/> class.
        /// </summary>
        /// <param name="result">The result of the query execution mapped to <see cref="DynamicObject" />'s.</param>
        internal QueryDataResult(IEnumerable<DynamicObject> result)
        {
            this.MappedResults = result;
        }

        /// <summary>
        ///     Gets or sets the result of the query execution mapped to <see cref="DynamicObject" />'s.
        /// </summary>
        [DataMember]
        public IEnumerable<DynamicObject> MappedResults { get; set; }

        /// <summary>
        ///     Convert this object into a string representation.
        /// </summary>
        /// <returns>
        ///     A string that represents this object.
        /// </returns>
        [ExcludeFromCoverage]
        public override string ToString()
            => $"Count = {this.MappedResults?.Count()}";
    }
}
