// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Net;
using System.Text.Json;
using InfoCarrier.Core.ValueMapping;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     Carries a <see cref="IPAddress" /> across the wire as its text form (ADR-012).
/// </summary>
/// <remarks>
///     <para>
///         <b>ADR-012's second consumer, and the one that shows the seam is not a spatial
///         feature.</b> The first was a geometry, whose members are computed and recurse; this is
///         a BCL type whose <c>ScopeId</c> getter <em>throws</em> <c>SocketException</c> for an
///         IPv4 address. Both are the same defect — a CLR type the reflective object walk must
///         not be pointed at — and one seam answers both.
///     </para>
///     <para>
///         B23 diagnosed the failure in full and found no narrow route at the time: routing such a
///         scalar through EF's core <c>ValueConverterSelector</c> inside
///         <c>PrimitiveCoercion.Coerce</c> fixes the test and costs <b>381</b>, because
///         <c>Coerce</c> is on every scalar path in the provider. The seam is the narrow way to
///         the same place — it is consulted only for a value that is not a wire primitive, not an
///         entity, and would otherwise be walked reflectively.
///     </para>
///     <para>
///         Registered test-side, like the geometry mapper, so the statement ADR-012 makes stays
///         true: the provider knows nothing about which CLR types an application carries. See
///         C23 in <c>docs/implementation-plan.md</c> for why a product default was <em>not</em>
///         taken here and what it would cost.
///     </para>
/// </remarks>
public sealed class InfoCarrierIPAddressValueMapper : IInfoCarrierValueMapper
{
    /// <inheritdoc />
    public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
    {
        if (value is not IPAddress address)
        {
            wireValue = null;
            return false;
        }

        // `ToString()` round-trips every family, including IPv6 with a scope id, and `Parse`
        // is its exact inverse. The property this is compared against carries EF's own
        // `IPAddress` value converter, which uses the same text form.
        wireValue = address.ToString();
        return true;
    }

    /// <inheritdoc />
    public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
    {
        value = null;

        if (!typeof(IPAddress).IsAssignableFrom(declaredType))
        {
            return false;
        }

        // After a serialization round-trip a wire primitive arrives as a `JsonElement`, exactly
        // as it does for every other primitive.
        string text = wireValue switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()!,
            _ => throw new InvalidOperationException(
                $"An IPAddress arrived on the wire as '{wireValue?.GetType().Name ?? "null"}', not as text."),
        };

        value = IPAddress.Parse(text);
        return true;
    }
}
