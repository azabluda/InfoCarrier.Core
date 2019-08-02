// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common.ValueMapping
{
    using System.Collections.Generic;

    /// <summary>
    /// Standard value mappers.
    /// </summary>
    public static partial class StandardValueMappers
    {
        /// <summary>
        /// Gets standard value mappers.
        /// </summary>
        public static IEnumerable<IInfoCarrierValueMapper> Mappers { get; } =
            new IInfoCarrierValueMapper[]
            {
                new ByteArrayValueMapper(),
                new ArrayValueMapper(),
                new GroupingValueMapper(),
                new EnumerableValueMapper(),
                new EntityValueMapper(),
            };
    }
}
