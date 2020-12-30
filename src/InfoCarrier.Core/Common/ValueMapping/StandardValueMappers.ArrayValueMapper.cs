// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common.ValueMapping
{
    using System;
    using System.Collections;
    using System.Linq;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;

    /// <summary>
    /// Standard value mapper for arrays.
    /// </summary>
    public static partial class StandardValueMappers
    {
        private class ArrayValueMapper : IInfoCarrierValueMapper
        {
            private const string ArrayType = @"ArrayType";
            private const string Elements = @"Elements";

            public bool TryMapToDynamicObject(IMapToDynamicObjectContext context, out object mapped)
            {
                Type objType = context.Object.GetType();
                if (!objType.IsArray)
                {
                    mapped = null;
                    return false;
                }

                var dto = new DynamicObject();
                context.AddToCache(dto);
                dto.Add(ArrayType, new TypeInfo(objType, includePropertyInfos: false));
                dto.Add(
                    Elements,
                    ((IEnumerable)context.Object).Cast<object>().Select(context.MapToDynamicObjectGraph).ToArray());

                mapped = dto;
                return true;
            }

            public bool TryMapFromDynamicObject(IMapFromDynamicObjectContext context, out object array)
            {
                array = null;

                if (context.Dto.Type != null)
                {
                    // Our custom mapping of arrays doesn't contain Type
                    return false;
                }

                if (!context.Dto.TryGet(ArrayType, out object arrayTypeObj))
                {
                    return false;
                }

                if (!context.Dto.TryGet(Elements, out object elements))
                {
                    return false;
                }

                if (arrayTypeObj is TypeInfo typeInfo)
                {
                    array = context.MapFromDynamicObjectGraph(elements, typeInfo.ResolveType(context.TypeResolver));
                    context.AddToCache(array);
                    return true;
                }

                return false;
            }
        }
    }
}
