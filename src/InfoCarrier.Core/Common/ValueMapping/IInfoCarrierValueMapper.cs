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
        /// and (if so) performs its mapping either to <see cref="DynamicObject"/> (if no further mapping is necessary)
        /// or to any other object which will then be passed to the standard <see cref="DynamicObjectMapper.MapToDynamicObjectGraph" />.
        /// </summary>
        /// <param name="context"> Mapping context. </param>
        /// <param name="mapped"> Mapping result. Null if this mapper isn't applicable. </param>
        /// <returns> True if this mapper is applicable. </returns>
        bool TryMapToDynamicObject(IMapToDynamicObjectContext context, out object mapped);

        /// <summary>
        /// Probes the <see cref="IMapFromDynamicObjectContext.Dto"/> whether it can be mapped by this mapper,
        /// and (if so) performs its mapping back to its normal representation.
        /// </summary>
        /// <param name="context"> Mapping context. </param>
        /// <param name="obj">
        ///     Mapping result, or a <see cref="DynamicObject"/> which will then be passed
        ///     to the standard <see cref="DynamicObjectMapper.MapFromDynamicObjectGraph" />.
        ///     Null if this mapper isn't applicable.
        /// </param>
        /// <returns> True if this mapper is applicable. </returns>
        bool TryMapFromDynamicObject(IMapFromDynamicObjectContext context, out object obj);
    }
}
