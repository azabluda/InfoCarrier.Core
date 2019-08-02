// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common.ValueMapping
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using InfoCarrier.Core.Properties;
    using TypeInfo = Aqua.TypeSystem.TypeInfo;

    /// <summary>
    /// Standard value mapper for collections other than arrays or IGrouping.
    /// </summary>
    public static partial class StandardValueMappers
    {
        private class EnumerableValueMapper : IInfoCarrierValueMapper
        {
            private const string CollectionType = @"CollectionType";
            private const string Elements = @"Elements";

            private static readonly System.Reflection.MethodInfo AddElementsToCollectionMethod
                = Utils.GetMethodInfo(() => AddElementsToCollection<object>(null, null))
                    .GetGenericMethodDefinition();

            public bool TryMapToDynamicObject(IMapToDynamicObjectContext context, out object mapped)
            {
                Type objType = context.Object.GetType();
                if (objType == typeof(string))
                {
                    mapped = null;
                    return false;
                }

                Type elementType = Utils.TryGetSequenceType(objType);
                if (elementType == null || elementType == typeof(DynamicObject))
                {
                    mapped = null;
                    return false;
                }

                var dto = new DynamicObject(typeof(IEnumerable<>).MakeGenericType(elementType));
                context.AddToCache(dto);
                dto.Add(
                    Elements,
                    new DynamicObject(((IEnumerable)context.Object).Cast<object>().Select(context.MapToDynamicObjectGraph).ToList()));

                if (objType != typeof(List<>).MakeGenericType(elementType))
                {
                    bool hasDefaultCtor = objType.GetTypeInfo().DeclaredConstructors
                        .Any(c => !c.IsStatic && c.IsPublic && c.GetParameters().Length == 0);
                    if (hasDefaultCtor)
                    {
                        dto.Add(CollectionType, new TypeInfo(objType, includePropertyInfos: false));
                    }
                }

                mapped = dto;
                return true;
            }

            public bool TryMapFromDynamicObject(IMapFromDynamicObjectContext context, out object collection)
            {
                collection = null;

                Type type = context.Dto.Type?.ResolveType(context.TypeResolver);

                if (type == null
                    || !type.IsGenericType
                    || type.GetGenericTypeDefinition() != typeof(IEnumerable<>))
                {
                    return false;
                }

                if (!context.Dto.TryGet(Elements, out object elements))
                {
                    return false;
                }

                Type elementType = type.GenericTypeArguments[0];

                // instantiate collection and add it to map
                Type resultType =
                    context.Dto.TryGet(CollectionType, out object collTypeObj) && collTypeObj is TypeInfo typeInfo
                        ? typeInfo.ResolveType(context.TypeResolver)
                        : typeof(OrderedQueryableList<>).MakeGenericType(elementType);
                collection = Activator.CreateInstance(resultType);
                context.AddToCache(collection);

                // map elements to list AFTER adding to map to avoid endless recursion
                Type listType = typeof(List<>).MakeGenericType(elementType);
                elements = context.MapFromDynamicObjectGraph(elements, listType);

                // copy elements from list to resulting collection
                try
                {
                    AddElementsToCollectionMethod.MakeGenericMethod(elementType).Invoke(null, new[] { collection, elements });
                }
                catch (TargetInvocationException e) when (e.InnerException != null)
                {
                    throw e.InnerException;
                }

                return true;
            }

            private static void AddElementsToCollection<TElement>(object collection, List<TElement> elements)
            {
                switch (collection)
                {
                    case ISet<TElement> set:
                        set.UnionWith(elements);
                        break;

                    case List<TElement> list:
                        list.AddRange(elements);
                        break;

                    case ICollection<TElement> coll:
                        foreach (var element in elements)
                        {
                            coll.Add(element);
                        }

                        break;

                    default:
                        throw new NotSupportedException(
                            InfoCarrierStrings.CannotAddElementsToCollection(
                                typeof(TElement),
                                collection.GetType()));
                }
            }

            private class OrderedQueryableList<T> : List<T>, IOrderedEnumerable<T>, IOrderedQueryable<T>
            {
                private readonly IQueryable<T> queryable;

                public OrderedQueryableList()
                {
                    this.queryable = new EnumerableQuery<T>(this);
                }

                public Type ElementType => queryable.ElementType;

                public System.Linq.Expressions.Expression Expression => queryable.Expression;

                public IQueryProvider Provider => queryable.Provider;

                public IOrderedEnumerable<T> CreateOrderedEnumerable<TKey>(
                    Func<T, TKey> keySelector,
                    IComparer<TKey> comparer,
                    bool descending)
                {
                    return descending ? this.OrderByDescending(keySelector, comparer) : this.OrderBy(keySelector, comparer);
                }
            }
        }
    }
}
