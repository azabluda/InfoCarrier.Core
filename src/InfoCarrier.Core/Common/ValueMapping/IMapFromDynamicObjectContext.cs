// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common.ValueMapping
{
    using System;
    using System.Collections.Generic;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;

    /// <summary>
    /// This interface defines the context for mapping a <see cref="DynamicObject"/> back to its normal representation.
    /// </summary>
    public interface IMapFromDynamicObjectContext
    {
        /// <summary>
        /// Gets a <see cref="DynamicObject"/> to map.
        /// </summary>
        DynamicObject Dto { get; }

        /// <summary>
        /// Gets a type resolver.
        /// </summary>
        ITypeResolver TypeResolver { get; }

        /// <summary>
        /// Maps an item of an object graph of <see cref="DynamicObject"/> back into its normal representation.
        /// This method is to be used for mapping properties of <see cref="Dto"/> recursively.
        /// </summary>
        /// <param name="obj"> <see cref="DynamicObject"/> to be mapped. </param>
        /// <param name="targetType"> Target type for mapping. </param>
        /// <returns> The object created based on the <see cref="DynamicObject"/> specified. </returns>
        object MapFromDynamicObjectGraph(object obj, Type targetType = null);

        /// <summary>
        /// Adds an object to internal identity cache to avoid endless recursion.
        ///
        /// This method MUST be called for an newly instantiated object corresponding to <see cref="Dto"/>
        /// as early as possible, e.g. for an instance of a collection BEFORE its elements are mapped and populated.
        /// </summary>
        /// <param name="obj"> Original object corresponding to <see cref="Dto"/>. </param>
        void AddToCache(object obj);

        /// <summary>
        /// Maps <see cref="Dto"/> as entity.
        /// </summary>
        /// <param name="entityTypeName"> Entity type name. </param>
        /// <param name="loadedNavigations">
        ///     List of loaded navigation properties of a tracked entity.
        ///     Null, if an entity is untracked.
        /// </param>
        /// <returns> Instance of an entity, or null if <paramref name="entityTypeName"/> is not a valid entity type name. </returns>
        object TryMapEntity(
            string entityTypeName,
            IReadOnlyList<string> loadedNavigations);
    }
}
