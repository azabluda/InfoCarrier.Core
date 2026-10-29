// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;

namespace InfoCarrier.WireContract;

internal static class ContractComparer
{
    internal static ComparisonResult Compare(WireContract previous, WireContract current, string reference)
    {
        var changes = new List<ContractChange>();
        var errors = ValidateAllocation(previous, current);
        if (string.IsNullOrWhiteSpace(reference) || previous.Types.Count == 0 || current.Types.Count == 0
            || previous.Frames.Count == 0 || current.Frames.Count == 0
            || previous.Inventory.Count == 0 || current.Inventory.Count == 0)
        {
            errors.Add("Missing baseline identity or empty contract inventory. Restore the complete comparison inputs.");
        }
        foreach (WireContract contract in new[] { previous, current })
        {
            foreach ((string path, TypeContract type) in contract.Types)
            {
                if (type.Direction == WireDirection.None || type.Children.Concat(type.Properties.Values.Select(p => p.Type))
                    .Any(child => !contract.Types.ContainsKey(child)))
                {
                    errors.Add($"Incomplete type graph at {path}; reference={reference}. Restore coverage for every child and direction.");
                }
            }
        }

        void Add(string path, WireDirection direction, ChangeKind kind, object? before, object? after, string reason)
            => changes.Add(new(path, direction, kind, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after), reason));

        foreach (string key in previous.Inventory.Keys.Union(current.Inventory.Keys).Order())
        {
            string? before = previous.Inventory.GetValueOrDefault(key), after = current.Inventory.GetValueOrDefault(key);
            if (before != after)
            {
                Add("inventory/" + key, WireDirection.Request | WireDirection.Response,
                    key.StartsWith("runtime:System.", StringComparison.Ordinal) ? ChangeKind.Compatible : ChangeKind.Unclassified,
                    before, after, "Inventory changed. Runtime version changes trigger inspection, not an automatic wire increase.");
            }
        }
        foreach (string key in previous.Types.Keys.Union(current.Types.Keys).Order())
        {
            previous.Types.TryGetValue(key, out TypeContract? before);
            current.Types.TryGetValue(key, out TypeContract? after);
            if (before is null)
            {
                Add(key, after!.Direction, ChangeKind.Compatible, null, after.Encoding,
                    "New covered type. Its use through properties and discriminator mappings is checked separately.");
                continue;
            }
            if (after is null)
            {
                Add(key, before.Direction, ChangeKind.Unclassified, before.Encoding, null,
                    "Registered type disappeared. Distinguish a harmless CLR rename from removal of a wire contract.");
                continue;
            }
            WireDirection direction = before.Direction | after.Direction;
            if (before.Direction != after.Direction || before.Kind != after.Kind || before.Encoding != after.Encoding)
            {
                Add(key, direction, ChangeKind.Unclassified,
                    new { before.Kind, before.Encoding, before.Direction }, new { after.Kind, after.Encoding, after.Direction },
                    "Type representation or covered direction changed; classify it explicitly.");
            }
            if (!Equal(before.Settings, after.Settings) || before.SkipUnknown != after.SkipUnknown)
            {
                Add(key + "/reader-writer-options", direction, ChangeKind.Unclassified,
                    new { before.Settings, before.SkipUnknown }, new { after.Settings, after.SkipUnknown },
                    "Changed serializer options require an explicit reader/writer compatibility rule.");
            }
            foreach (string mapping in before.Mappings.Keys.Union(after.Mappings.Keys).Order())
            {
                string? oldValue = before.Mappings.GetValueOrDefault(mapping), newValue = after.Mappings.GetValueOrDefault(mapping);
                if (oldValue == newValue)
                {
                    continue;
                }
                bool addition = oldValue is null;
                Add(key + "/" + mapping, direction,
                    addition && !direction.HasFlag(WireDirection.Response) ? ChangeKind.Compatible : ChangeKind.Incompatible,
                    oldValue, newValue, addition
                        ? "New input variants preserve older requests; older response readers are not assumed to understand new variants."
                        : "Existing discriminator or enum assignments must retain their identities.");
            }
            foreach (string name in before.Properties.Keys.Union(after.Properties.Keys).Order())
            {
                string path = key + "/" + name;
                before.Properties.TryGetValue(name, out PropertyContract? oldProperty);
                after.Properties.TryGetValue(name, out PropertyContract? newProperty);
                if (oldProperty is null)
                {
                    if (direction.HasFlag(WireDirection.Request))
                    {
                        Add(path, WireDirection.Request, newProperty!.Required ? ChangeKind.Incompatible : ChangeKind.Compatible,
                            null, newProperty, newProperty.Required
                                ? "An older request cannot supply this new required input."
                                : "The current reader permits omission of this new property.");
                    }
                    if (direction.HasFlag(WireDirection.Response))
                    {
                        Add(path, WireDirection.Response, before.SkipUnknown ? ChangeKind.Compatible : ChangeKind.Incompatible,
                            null, newProperty, "Additional response properties require older readers to skip unknown properties.");
                    }
                    continue;
                }
                if (newProperty is null)
                {
                    Add(path, direction, ChangeKind.Incompatible, oldProperty, null,
                        "The conservative field-preservation rule forbids removal or renaming under the same version, even when parsing could succeed.");
                    continue;
                }
                if (oldProperty.Type != newProperty.Type)
                {
                    string? oldEncoding = previous.Types.GetValueOrDefault(oldProperty.Type)?.Encoding;
                    string? newEncoding = current.Types.GetValueOrDefault(newProperty.Type)?.Encoding;
                    bool differentTokens = Token(oldEncoding) is { } oldToken && Token(newEncoding) is { } newToken
                        && oldToken != newToken;
                    Add(path, direction, differentTokens ? ChangeKind.Incompatible : ChangeKind.Unclassified,
                        oldProperty.Type, newProperty.Type, differentTokens
                            ? "The JSON token representation changed."
                            : "Changed field types need explicit classification; numeric widening and CLR renames are not inferred.");
                }
                if (direction.HasFlag(WireDirection.Request) && oldProperty.Required != newProperty.Required)
                {
                    Add(path + "/required", WireDirection.Request,
                        newProperty.Required ? ChangeKind.Incompatible : ChangeKind.Compatible,
                        oldProperty.Required, newProperty.Required, "Requiredness controls whether the current reader accepts older requests.");
                }
                if (oldProperty.Readable != newProperty.Readable || oldProperty.Writable != newProperty.Writable
                    || oldProperty.Type == newProperty.Type && !Equal(oldProperty.Settings, newProperty.Settings))
                {
                    Add(path + "/access-and-options", direction, ChangeKind.Unclassified, oldProperty, newProperty,
                        "Property access or encoding options changed; complete their classification.");
                }
            }
            // Enumerable and nullable children are not represented by object properties or discriminator mappings.
            if (before.Kind != "Object" && !before.Children.SequenceEqual(after.Children))
            {
                Add(key + "/elements", direction, ChangeKind.Unclassified, before.Children, after.Children,
                    "Collection or nullable element representation changed.");
            }
        }
        foreach (string frame in previous.Frames.Keys.Union(current.Frames.Keys).Order())
        {
            previous.Frames.TryGetValue(frame, out string[]? before);
            current.Frames.TryGetValue(frame, out string[]? after);
            if (before is null || after is null || !before.SequenceEqual(after))
            {
                Add("frames/" + frame, WireDirection.Response, ChangeKind.Incompatible, before, after,
                    "The exercised frame tag, length, payload identity, or position changed.");
            }
        }
        return new(reference, previous.Version, current.Version, changes, errors);
    }

    private static List<string> ValidateAllocation(WireContract previous, WireContract current)
    {
        var errors = new List<string>();
        foreach (WireContract contract in new[] { previous, current })
        {
            int[] history = contract.AllocatedVersions;
            if (history.Length == 0 || history.Any(n => n <= 0)
                || !history.SequenceEqual(history.Distinct().Order()) || contract.Version != history.LastOrDefault())
            {
                errors.Add("Allocation history must be positive, increasing, and end at the authoritative current wire version.");
            }
        }
        if (!current.AllocatedVersions.Take(previous.AllocatedVersions.Length).SequenceEqual(previous.AllocatedVersions))
        {
            errors.Add("Allocated integers cannot be removed, replaced, or reused. Restore the protected allocation history.");
        }
        if (current.Version < previous.Version
            || current.Version != previous.Version && current.Version <= previous.AllocatedVersions.DefaultIfEmpty(0).Max())
        {
            errors.Add("The current version rolled back or reused an allocated integer. Select a fresh larger integer.");
        }
        return errors;
    }

    private static bool Equal(Dictionary<string, string> left, Dictionary<string, string> right)
        => left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out string? value) && value == p.Value);

    private static string? Token(string? encoding) => encoding switch
    {
        "scalar/System.String" or "scalar/System.Char" or "scalar/base64" => "string",
        "scalar/System.Boolean" => "boolean",
        "scalar/System.Byte" or "scalar/System.SByte" or "scalar/System.Int16" or "scalar/System.UInt16"
            or "scalar/System.Int32" or "scalar/System.UInt32" or "scalar/System.Int64" or "scalar/System.UInt64"
            or "scalar/System.Single" or "scalar/System.Double" or "scalar/System.Decimal" => "number",
        _ => null,
    };
}
