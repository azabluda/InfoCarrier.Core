// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common.ValueMapping
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;

    /// <summary>
    /// Standard value mapper for IGrouping.
    /// </summary>
    public static partial class StandardValueMappers
    {
        private class GroupingValueMapper : IInfoCarrierValueMapper
        {
            private const string Key = @"Key";
            private const string Elements = @"Elements";

            private static readonly System.Reflection.MethodInfo MakeGenericGroupingMethod
                = Utils.GetMethodInfo(() => MakeGenericGrouping<object, object>(null, null))
                    .GetGenericMethodDefinition();

            public bool TryMapToDynamicObject(IMapToDynamicObjectContext context, out object mapped)
            {
                var groupingType = Utils.GetGenericTypeImplementations(context.Object.GetType(), typeof(IGrouping<,>)).SingleOrDefault();

                if (groupingType == null)
                {
                    mapped = null;
                    return false;
                }

                var dto = new DynamicObject(groupingType);
                context.AddToCache(dto);
                dto.Add(Key, context.MapToDynamicObjectGraph(groupingType.GetProperty(Key)?.GetValue(context.Object)));
                dto.Add(
                    Elements,
                    new DynamicObject(((IEnumerable)context.Object).Cast<object>().Select(context.MapToDynamicObjectGraph).ToList()));

                mapped = dto;
                return true;
            }

            public bool TryMapFromDynamicObject(IMapFromDynamicObjectContext context, out object grouping)
            {
                grouping = null;

                Type type = context.Dto.Type?.ResolveType(context.TypeResolver);

                if (type == null
                    || !type.IsGenericType
                    || type.GetGenericTypeDefinition() != typeof(IGrouping<,>))
                {
                    return false;
                }

                if (!context.Dto.TryGet(Key, out object key))
                {
                    return false;
                }

                if (!context.Dto.TryGet(Elements, out object elements))
                {
                    return false;
                }

                Type keyType = type.GenericTypeArguments[0];
                Type elementType = type.GenericTypeArguments[1];

                key = context.MapFromDynamicObjectGraph(key, keyType);
                elements = context.MapFromDynamicObjectGraph(elements, typeof(List<>).MakeGenericType(elementType));

                grouping = MakeGenericGroupingMethod
                    .MakeGenericMethod(keyType, elementType)
                    .Invoke(null, new[] { key, elements });
                context.AddToCache(grouping);

                return true;
            }

            private static IGrouping<TKey, TElement> MakeGenericGrouping<TKey, TElement>(TKey key, IEnumerable<TElement> elements)
            {
                return elements.GroupBy(x => key).Single();
            }
        }
    }
}
