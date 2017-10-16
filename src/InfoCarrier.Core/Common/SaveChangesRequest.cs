// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore.Update;

    /// <summary>
    ///     A serializable object containing the <see cref="UpdateEntryDto" />'s for saving
    ///     updated entities into the actual database on the server-side.
    /// </summary>
    [DataContract]
    public class SaveChangesRequest
    {
        private IDynamicObjectMapper mapper;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SaveChangesRequest"/> class.
        /// </summary>
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public SaveChangesRequest()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SaveChangesRequest"/> class.
        /// </summary>
        /// <param name="entries">The <see cref="IUpdateEntry" />'s which need to be saved.</param>
        internal SaveChangesRequest(IEnumerable<IUpdateEntry> entries)
        {
            this.DataTransferObjects.AddRange(entries.Select(e => new UpdateEntryDto(e, this.Mapper)));
        }

        /// <summary>
        ///     Gets the <see cref="IDynamicObjectMapper" /> instance which is used internally for mapping
        ///     scalar properties of <see cref="IUpdateEntry" />'s to/from <see cref="DynamicObject" />.
        /// </summary>
        [IgnoreDataMember]
        internal IDynamicObjectMapper Mapper
            => this.mapper ?? (this.mapper = new DynamicObjectMapper(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true }));

        /// <summary>
        ///     Gets or sets state entires mapped to <see cref="UpdateEntryDto" />'s.
        /// </summary>
        [DataMember]
        public List<UpdateEntryDto> DataTransferObjects { get; set; } = new List<UpdateEntryDto>();
    }
}
