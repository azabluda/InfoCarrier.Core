// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Northwind.Client.Wire;

/// <summary>
///     Turns an envelope's payload into something a person can read.
/// </summary>
/// <remarks>
///     <para>
///         The interesting part is not the payload itself but what is <em>inside</em> it. The wire
///         is layered on purpose: an envelope's <c>Payload</c> is a <c>byte[]</c> holding the JSON
///         of an operation record, and several of that record's own members —
///         <c>SerializedQuery</c>, <c>SerializedResults</c>, <c>SerializedValues</c> — are again
///         <c>byte[]</c>, holding the expression tree and the row data. System.Text.Json renders a
///         <c>byte[]</c> as base64, so a naive dump shows one line of gibberish where the
///         expression tree ought to be. Phase 1 learned this the expensive way: a test asserting
///         against the raw body could not fail, because the base64 alphabet contains no <c>-</c>
///         and the row data was two layers further in.
///     </para>
///     <para>
///         So this expands every nested layer, and does it <b>structurally rather than by member
///         name</b>: any string that decodes as base64 <em>and</em> parses as JSON is replaced by
///         the JSON it holds. Nothing here knows the name of a single wire member, which is what
///         keeps it working when one is added.
///     </para>
/// </remarks>
internal static class WireDecoder
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    ///     Renders <paramref name="payload" /> as indented JSON with every nested payload
    ///     expanded in place. Returns the raw text if it is not JSON at all, because showing what
    ///     actually arrived beats showing an error about it.
    /// </summary>
    public static string Describe(byte[]? payload)
    {
        if (payload is null || payload.Length == 0)
        {
            return "(empty)";
        }

        string text = Encoding.UTF8.GetString(payload);

        try
        {
            JsonNode? node = JsonNode.Parse(text);
            if (node is null)
            {
                return "null";
            }

            return (Replacement(node) ?? node).ToJsonString(Indented);
        }
        catch (Exception exception)
        {
            // Deliberately broad, and it is not a substitute for getting this right. The panel is
            // a debugging aid observing an operation it must never be able to break -- and it did
            // break one: the first version reassigned an already-parented node, and every query on
            // the page died with `InvalidOperationException: NodeAlreadyHasParent` raised from
            // inside the decorator. The bug is fixed below; this makes the class of bug
            // non-fatal, and says so on screen rather than silently showing less.
            return $"(could not decode: {exception.GetType().Name}: {exception.Message})\n\n{text}";
        }
    }

    /// <summary>
    ///     Returns a node to put in <paramref name="node" />'s place, or null when there is
    ///     nothing to replace — either because it needed no expansion, or because it was expanded
    ///     in place.
    /// </summary>
    /// <remarks>
    ///     <b>Null rather than "the node itself" matters.</b> A <see cref="JsonNode" /> may have
    ///     only one parent, so assigning an already-parented node back into its own slot throws
    ///     <c>InvalidOperationException: NodeAlreadyHasParent</c>. Returning the node unchanged and
    ///     letting the caller assign it is therefore not a harmless no-op — it is the bug this
    ///     shape exists to prevent.
    /// </remarks>
    private static JsonNode? Replacement(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                // ToList: the loop reassigns members, and mutating a JsonObject while enumerating
                // it throws.
                foreach (KeyValuePair<string, JsonNode?> member in obj.ToList())
                {
                    if (member.Value is { } value && Replacement(value) is { } replacement)
                    {
                        obj[member.Key] = replacement;
                    }
                }

                return null;

            case JsonArray array:
                for (int i = 0; i < array.Count; i++)
                {
                    if (array[i] is { } item && Replacement(item) is { } replacement)
                    {
                        array[i] = replacement;
                    }
                }

                return null;

            case JsonValue value when value.TryGetValue(out string? text) && text is not null:
                return Nested(text);

            default:
                return null;
        }
    }

    /// <summary>
    ///     Returns the JSON a base64 string holds, or null if it is not a nested payload.
    /// </summary>
    /// <remarks>
    ///     Two filters, and both matter. Short strings are skipped because plenty of ordinary
    ///     values ("ALFKI") are accidentally valid base64 and decode to bytes that are not JSON —
    ///     the JSON parse would reject them anyway, but only after a decode per value. And the
    ///     decoded text must parse as an object or array: a bare number or string would let
    ///     "1234" masquerade as a nested payload.
    /// </remarks>
    private static JsonNode? Nested(string text)
    {
        if (text.Length < 8)
        {
            return null;
        }

        // Base64 never expands, so the source length is always a safe upper bound.
        byte[] decoded = new byte[text.Length];
        if (!Convert.TryFromBase64String(text, decoded, out int written))
        {
            return null;
        }

        try
        {
            JsonNode? inner = JsonNode.Parse(Encoding.UTF8.GetString(decoded, 0, written));
            if (inner is not (JsonObject or JsonArray))
            {
                return null;
            }

            // Freshly parsed, so it has no parent and the caller can safely adopt it. Expanding
            // its own contents first is what makes the walk reach every layer.
            Replacement(inner);
            return inner;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
