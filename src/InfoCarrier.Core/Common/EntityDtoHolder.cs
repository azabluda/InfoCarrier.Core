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
    ///     A serializable object containing the <see cref="UpdateEntryDto" />'s.
    /// </summary>
    [DataContract]
    public abstract class EntityDtoHolder
    {
        private IDynamicObjectMapper mapper;

        /// <summary>
        ///     Initializes a new instance of the <see cref="EntityDtoHolder"/> class.
        /// </summary>
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public EntityDtoHolder()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EntityDtoHolder"/> class.
        /// </summary>
        /// <param name="entries">The <see cref="IUpdateEntry" />'s which need to be saved.</param>
        internal EntityDtoHolder(IReadOnlyList<IUpdateEntry> entries)
        {
            this.DataTransferObjects.AddRange(this.ToUpdateEntryDtos(entries));
        }

        /// <summary>
        ///     Gets the <see cref="IDynamicObjectMapper" /> instance which is used internally for mapping
        ///     scalar properties of <see cref="IUpdateEntry" />'s to/from <see cref="DynamicObject" />.
        /// </summary>
        [IgnoreDataMember]
        internal IDynamicObjectMapper Mapper
            => this.mapper ?? (this.mapper = new DynamicObjectMapper(new DynamicObjectMapperSettings { FormatNativeTypesAsString = true }));

        /// <summary>
        ///     Gets or sets state entires mapped to <see cref="UpdateEntryDto" />'s.
        /// </summary>
        [DataMember]
        public List<UpdateEntryDto> DataTransferObjects { get; set; } = new List<UpdateEntryDto>();

        /// <summary>
        ///     Convert this object into a string representation.
        /// </summary>
        /// <returns>
        ///     A string that represents this object.
        /// </returns>
        [ExcludeFromCoverage]
        public override string ToString()
            => $"Count = {this.DataTransferObjects?.Count()}";

        /// <summary>
        ///     Converts <see cref="IUpdateEntry" />'s to/from <see cref="UpdateEntryDto" />'s.
        /// </summary>
        /// <param name="entries"> The <see cref="IUpdateEntry" />'s which need to be converted to DTOs. </param>
        /// <returns> Converted <see cref="UpdateEntryDto" />'s. </returns>
        protected IEnumerable<UpdateEntryDto> ToUpdateEntryDtos(IEnumerable<IUpdateEntry> entries)
            => entries.Select(e => new UpdateEntryDto(e, this.Mapper));
    }
}
