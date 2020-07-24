// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using InfoCarrier.Core.Common.ValueMapping;
    using NetTopologySuite.Geometries;
    using NetTopologySuite.IO;

    public class InfoCarrierNetTopologySuiteValueMapper : IInfoCarrierValueMapper
    {
        private const string Data = @"Data";

        public bool TryMapToDynamicObject(IMapToDynamicObjectContext context, out object mapped)
        {
            if (!(context.Object is Geometry geometry))
            {
                mapped = null;
                return false;
            }

            var dto = new DynamicObject(typeof(Geometry));
            context.AddToCache(dto);

            dto.Add(Data, new GeoJsonWriter().Write(geometry));

            mapped = dto;
            return true;
        }

        public bool TryMapFromDynamicObject(IMapFromDynamicObjectContext context, out object obj)
        {
            if (context.Dto.Type.ResolveType(context.TypeResolver) != typeof(Geometry))
            {
                obj = null;
                return false;
            }

            if (!context.Dto.TryGet(Data, out object jsonObj) || !(jsonObj is string json))
            {
                obj = null;
                return false;
            }

            obj = new GeoJsonReader().Read<Geometry>(json);
            context.AddToCache(obj);
            return true;
        }
    }
}
