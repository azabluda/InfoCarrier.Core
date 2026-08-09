// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using InfoCarrier.Core.ValueMapping;
using NetTopologySuite.Geometries;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Expressions;

/// <summary>
///     The wire form of a geometry, asserted directly.
/// </summary>
/// <remarks>
///     The two spatial spec suites would not catch a regression here: their model is XY at SRID
///     0, so a mapper that silently dropped Z, M and SRID would pass every one of their ~173
///     tests. Requirements §2.8 exists because v1 did exactly that — it used GeoJSON, which has
///     no Z or M ordinate at all — so the ordinates are asserted where losing them is visible.
/// </remarks>
public class GeometryWireFormatTest
{
    private static readonly IInfoCarrierValueMapper Mapper = new InfoCarrierNetTopologySuiteValueMapper();

    [ConditionalFact]
    public void A_point_survives_with_its_Z_and_M_ordinates()
    {
        GeometryFactory factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        Point point = factory.CreatePoint(new CoordinateZM(1, 2, 3, 4));

        var read = (Point)RoundTrip(point);

        Assert.Equal(1, read.Coordinate.X);
        Assert.Equal(2, read.Coordinate.Y);
        Assert.Equal(3, read.Coordinate.Z);
        Assert.Equal(4, read.Coordinate.M);
        Assert.Equal(4326, read.SRID);
    }

    [ConditionalFact]
    public void A_plain_XY_geometry_round_trips_unchanged()
    {
        GeometryFactory factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 0);
        LineString line = factory.CreateLineString([new Coordinate(0, 0), new Coordinate(1, 1)]);

        Geometry read = RoundTrip(line);

        Assert.True(line.EqualsExact(read));
        Assert.Equal(0, read.SRID);
    }

    [ConditionalFact]
    public void The_mapper_declines_everything_that_is_not_a_geometry()
    {
        Assert.False(Mapper.TryMapToWire("a string", typeof(string), out _));
        Assert.False(Mapper.TryMapToWire(42, typeof(int), out _));
        Assert.False(Mapper.TryMapFromWire("POINT (1 2)", typeof(string), out _));
    }

    private static Geometry RoundTrip(Geometry geometry)
    {
        Assert.True(Mapper.TryMapToWire(geometry, geometry.GetType(), out object? wire));
        Assert.True(Mapper.TryMapFromWire(wire, geometry.GetType(), out object? read));
        return (Geometry)read!;
    }
}
