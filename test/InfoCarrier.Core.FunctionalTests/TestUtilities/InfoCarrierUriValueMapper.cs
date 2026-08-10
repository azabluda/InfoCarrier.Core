// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using InfoCarrier.Core.ValueMapping;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

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
///         Test-side, like the other two. **Whether the product should ship standard mappers for
///         BCL types like this one is an open decision** — see C23 and C34 in
///         `docs/implementation-plan.md`. Three instances now argue for it; the counter-argument
///         is that ADR-012 states registration is the application's.
///     </para>
/// </remarks>
public sealed class InfoCarrierUriValueMapper : IInfoCarrierValueMapper
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
