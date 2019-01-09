// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common.ValueMapping
{
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;

    /// <summary>
    /// This interface defines the context for mapping an object to a <see cref="DynamicObject"/> suitable for serialization.
    /// </summary>
    public interface IMapToDynamicObjectContext
    {
        /// <summary>
        /// Gets an object to map.
        /// </summary>
        object Object { get; }

        /// <summary>
        /// Gets EF EntityEntry if the <see cref="Object"/> is an valid entity.
        /// </summary>
        InternalEntityEntry EntityEntry { get; }

        /// <summary>
        /// This method is to be used for mapping properties of <see cref="Object"/> recursively.
        /// </summary>
        /// <param name="obj"> An object to be mapped. </param>
        /// <returns> The <see cref="DynamicObject"/> suitable for serialization. </returns>
        DynamicObject MapToDynamicObjectGraph(object obj);

        /// <summary>
        /// Adds an object to internal identity cache to avoid endless recursion.
        ///
        /// This method MUST be called for a newly instantiated <see cref="DynamicObject"/>
        /// representing the <see cref="Object"/> as early as possible, e.g. for an instance of
        /// a collection BEFORE its elements are mapped.
        /// </summary>
        /// <param name="dynamicObject"> A <see cref="DynamicObject"/> representing the <see cref="Object"/>. </param>
        void AddToCache(DynamicObject dynamicObject);
    }
}
