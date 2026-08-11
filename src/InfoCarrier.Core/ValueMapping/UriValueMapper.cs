// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;

namespace InfoCarrier.Core.ValueMapping;

/// <summary>
///     Carries a <see cref="Uri" /> across the wire as its original string (ADR-012).
/// </summary>
/// <remarks>
///     <para>
///         **ADR-012's third consumer, and the third distinct reason for the same seam.** A
///         geometry's members recurse; <see cref="System.Net.IPAddress" />'s <c>ScopeId</c> throws
///         for an IPv4 address; and <see cref="Uri" />'s <c>AbsolutePath</c> throws
///         <c>InvalidOperationException: This operation is not supported for a relative URI</c> —
///         which is what `Insert_update_and_delete_with_wrapped_Uri_key` hit, through
///         `RuntimeMethodInfo.Invoke`, in the reflective object-shape walk.
///     </para>
///     <para>
///         <c>OriginalString</c> and the <c>RelativeOrAbsolute</c> constructor are exact inverses
///         and are the only pair that is: <c>ToString()</c> unescapes, and <c>AbsoluteUri</c>
///         throws for exactly the instances this exists to carry.
///     </para>
///     <para>
///         <b>Shipped, and registered by default</b> (C89, ADR-012 amended). A <see cref="Uri" />
///         is a BCL type: an application that stores one has not opted into anything exotic and
///         should not have to discover this seam to make it work. The geometry mapper is
///         <em>not</em> shipped, and that asymmetry is the decision rather than an oversight — it
///         would put a NetTopologySuite dependency in this package for a type most callers never
///         use. See <c>docs/decisions.md</c> ADR-012 for how an application registers one.
///     </para>
/// </remarks>
public sealed class UriValueMapper : IInfoCarrierValueMapper
{
    /// <inheritdoc />
    public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
    {
        if (value is not Uri uri)
        {
            wireValue = null;
            return false;
        }

        wireValue = uri.OriginalString;
        return true;
    }

    /// <inheritdoc />
    public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
    {
        value = null;

        if (!typeof(Uri).IsAssignableFrom(declaredType))
        {
            return false;
        }

        string text = wireValue switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()!,
            _ => throw new InvalidOperationException(
                $"A Uri arrived on the wire as '{wireValue?.GetType().Name ?? "null"}', not as text."),
        };

        // `RelativeOrAbsolute`, because a relative URI is the case this mapper exists for.
        value = new Uri(text, UriKind.RelativeOrAbsolute);
        return true;
    }
}
