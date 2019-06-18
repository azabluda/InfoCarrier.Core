// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common.ValueMapping
{
    using Aqua.TypeSystem;

    /// <summary>
    /// Standard value mapper for arrays of bytes.
    /// </summary>
    public static partial class StandardValueMappers
    {
        private class ByteArrayValueMapper : IInfoCarrierValueMapper
        {
            public bool TryMapToDynamicObject(IMapToDynamicObjectContext context, out object mapped)
            {
                if (context.Object.GetType() == typeof(byte[]))
                {
                    mapped = context.Object;
                    return true;
                }

                mapped = null;
                return false;
            }

            public bool TryMapFromDynamicObject(IMapFromDynamicObjectContext context, out object obj)
            {
                if (context.Dto.Type?.ResolveType(context.TypeResolver) == typeof(byte[]))
                {
                    obj = context.Dto;
                    return true;
                }

                obj = null;
                return false;
            }
        }
    }
}
