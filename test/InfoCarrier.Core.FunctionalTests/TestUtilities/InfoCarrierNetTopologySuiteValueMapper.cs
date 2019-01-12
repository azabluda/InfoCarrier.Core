// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using GeoAPI.Geometries;
    using InfoCarrier.Core.Common.ValueMapping;
    using NetTopologySuite.IO;

    public class InfoCarrierNetTopologySuiteValueMapper : IInfoCarrierValueMapper
    {
        private const string Data = @"Data";

        public bool TryMapToDynamicObject(IMapToDynamicObjectContext context, out DynamicObject dto)
        {
            if (!(context.Object is IGeometry geometry))
            {
                dto = null;
                return false;
            }

            dto = new DynamicObject(typeof(IGeometry));
            context.AddToCache(dto);

            dto.Add(Data, new GeoJsonWriter().Write(geometry));
            return true;
        }

        public bool TryMapFromDynamicObject(IMapFromDynamicObjectContext context, out object obj)
        {
            if (context.Dto.Type.ResolveType(context.TypeResolver) != typeof(IGeometry))
            {
                obj = null;
                return false;
            }

            if (!context.Dto.TryGet(Data, out object jsonObj) || !(jsonObj is string json))
            {
                obj = null;
                return false;
            }

            obj = new GeoJsonReader().Read<IGeometry>(json);
            context.AddToCache(obj);
            return true;
        }
    }
}
