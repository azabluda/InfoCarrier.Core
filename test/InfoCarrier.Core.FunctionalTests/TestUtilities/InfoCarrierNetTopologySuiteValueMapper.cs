// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Globalization;
using System.Text.Json;
using InfoCarrier.Core.ValueMapping;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     Carries a NetTopologySuite geometry across the wire as one WKT string (ADR-012).
/// </summary>
/// <remarks>
///     <para>
///         <strong>This lives test-side on purpose.</strong> v1 did the same, and it is why
///         neither v1's nor this provider's product assembly has ever referenced
///         NetTopologySuite: an application that wants geometries supplies the mapper, and the
///         seam is the whole of what the provider knows. The only spatial code in <c>src/</c> is
///         C15's type-mapping branch, which matches a type name as a <em>string</em>.
///     </para>
///     <para>
///         <strong>WKT, not v1's GeoJSON.</strong> GeoJSON carries no Z and no M ordinate — that
///         is the defect requirements §2.8 records as "v1 lost them", and roadmap M7 Q7 answers
///         with WKT. Adopting v1's mechanism while repeating v1's format would be the one
///         avoidable mistake available here, so the writer is configured for XYZM and the
///         round-trip is asserted directly in <c>GeometryWireFormatTest</c> rather than left to
///         the spatial suites, whose own model is XY at SRID 0 and would never notice.
///     </para>
///     <para>
///         SRID rides along as an EWKT <c>SRID=n;</c> prefix, which NetTopologySuite's own reader
///         and writer are the two halves of.
///     </para>
/// </remarks>
public sealed class InfoCarrierNetTopologySuiteValueMapper : IInfoCarrierValueMapper
{
    private static readonly WKTReader Reader = new();

    /// <inheritdoc />
    public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
    {
        if (value is not Geometry geometry)
        {
            wireValue = null;
            return false;
        }

        // A new writer per call: `WKTWriter` is not documented as thread-safe, and this is on
        // the per-value path of a provider whose server may be serving several requests.
        var writer = new WKTWriter(4) { OutputOrdinates = Ordinates.XYZM };

        // The `SRID=n;` prefix is written by hand rather than by the writer: NTS 2.6's
        // `WKTWriter` has no SRID switch, and an EWKT prefix this mapper writes and reads is
        // one round-trip whose two halves are visibly the same.
        wireValue = $"SRID={geometry.SRID.ToString(CultureInfo.InvariantCulture)};{writer.Write(geometry)}";
        return true;
    }

    /// <inheritdoc />
    public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
    {
        value = null;

        if (!typeof(Geometry).IsAssignableFrom(declaredType))
        {
            return false;
        }

        // After a serialization round-trip a wire primitive arrives as a `JsonElement`, exactly
        // as `PrimitiveCoercion.Coerce` has to deal with for every other primitive. Declining a
        // shape we cannot read would be worse than failing: the value would fall through to the
        // reflective walk this mapper exists to prevent, and that walk is what aborts the host.
        string wkt = wireValue switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()!,
            _ => throw new InvalidOperationException(
                $"A geometry arrived on the wire as '{wireValue?.GetType().Name ?? "null"}', not as WKT text."),
        };

        int srid = 0;
        if (wkt.StartsWith("SRID=", StringComparison.Ordinal) && wkt.IndexOf(';') is int semicolon and > 0)
        {
            srid = int.Parse(wkt[5..semicolon], CultureInfo.InvariantCulture);
            wkt = wkt[(semicolon + 1)..];
        }

        var geometry = Reader.Read(wkt);
        geometry.SRID = srid;
        value = geometry;
        return true;
    }
}
