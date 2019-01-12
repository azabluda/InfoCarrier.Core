// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common.ValueMapping
{
    using Aqua.Dynamic;

    /// <summary>
    /// The value mapper interface. It is required when you are creating your own mapper
    /// between values of a certain type (or group of types) and the corresponding <see cref="DynamicObject"/>s
    /// suitable for serialization.
    /// </summary>
    public interface IInfoCarrierValueMapper
    {
        /// <summary>
        /// Probes the <see cref="IMapToDynamicObjectContext.Object"/> whether it can be mapped by this mapper,
        /// and (if so) performs its mapping to <see cref="DynamicObject"/>.
        /// </summary>
        /// <param name="context"> Mapping context. </param>
        /// <param name="dto"> Mapping result. Null if this mapper isn't applicable. </param>
        /// <returns> True if this mapper has actually mapped the object. </returns>
        bool TryMapToDynamicObject(IMapToDynamicObjectContext context, out DynamicObject dto);

        /// <summary>
        /// Probes the <see cref="IMapFromDynamicObjectContext.Dto"/> whether it can be mapped by this mapper,
        /// and (if so) performs its mapping back to its normal representation.
        /// </summary>
        /// <param name="context"> Mapping context. </param>
        /// <param name="obj"> Mapping result. Null if this mapper isn't applicable. </param>
        /// <returns> True if this mapper has actually mapped the <see cref="DynamicObject"/>. </returns>
        bool TryMapFromDynamicObject(IMapFromDynamicObjectContext context, out object obj);
    }
}
