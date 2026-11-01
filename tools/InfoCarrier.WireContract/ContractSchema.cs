// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace InfoCarrier.WireContract;

/// <summary>Stores the covered contract in a bounded JSON Schema 2020-12 catalogue.</summary>
/// <remarks>This is not a general schema parser or a proof of arbitrary serializer behavior.</remarks>
internal static class ContractSchema
{
    internal const string Dialect = "https://json-schema.org/draft/2020-12/schema";
    internal const string Extension = "x-infocarrier";

    internal static JsonObject Write(WireContract contract)
    {
        var definitions = new JsonObject();
        foreach ((string key, TypeContract type) in contract.Types.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var shape = Shape(type);
            shape[Extension] = new JsonObject
            {
                ["kind"] = type.Kind,
                ["encoding"] = type.Encoding,
                ["direction"] = (int)type.Direction,
                ["settings"] = Node(type.Settings),
                ["enumAssignments"] = Node(type.Mappings.Where(p => p.Key.StartsWith("enum:", StringComparison.Ordinal))
                    .ToDictionary(p => p.Key, p => p.Value)),
            };
            if (type.Kind == "Object")
            {
                var properties = new JsonObject();
                foreach ((string name, PropertyContract property) in type.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    properties[name] = new JsonObject
                    {
                        ["$ref"] = Reference("type:" + property.Type),
                        [Extension] = new JsonObject
                        {
                            ["readable"] = property.Readable,
                            ["writable"] = property.Writable,
                            ["settings"] = Node(property.Settings),
                        },
                    };
                }
                shape["properties"] = properties;
                shape["required"] = Node(type.Properties.Where(p => p.Value.Required).Select(p => p.Key).Order(StringComparer.Ordinal).ToArray());
                shape["additionalProperties"] = type.SkipUnknown;
                if (type.Mappings.Keys.Any(k => k.StartsWith("kind:", StringComparison.Ordinal)))
                {
                    var variants = new JsonArray { new JsonObject { ["type"] = "null" } };
                    foreach ((string tag, string target) in type.Mappings.Where(p => p.Key.StartsWith("kind:", StringComparison.Ordinal))
                        .OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        if (!type.SkipUnknown
                            || contract.Types.TryGetValue(target, out TypeContract? derived) && !derived.SkipUnknown)
                            throw Incomplete(target, "Polymorphic readers that forbid additional fields require explicit schema coverage");
                        variants.Add(new JsonObject
                        {
                            ["allOf"] = new JsonArray
                            {
                                new JsonObject { ["$ref"] = Reference("type:" + target) },
                                new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        [type.Settings["discriminatorProperty"]] = new JsonObject { ["const"] = JsonNode.Parse(tag[5..]) },
                                    },
                                    ["required"] = Node(new[] { type.Settings["discriminatorProperty"] }),
                                },
                            },
                        });
                    }
                    shape["anyOf"] = variants;
                }
            }
            definitions.Add("type:" + key, shape);
        }
        foreach ((string name, string[] layout) in contract.Frames.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var positions = new JsonArray();
            foreach (string position in layout)
            {
                int colon = position.IndexOf(':');
                string field = position[..colon], encoding = position[(colon + 1)..];
                var item = new JsonObject { [Extension] = new JsonObject { ["position"] = field } };
                switch (field)
                {
                    case "Tag": item["const"] = JsonNode.Parse(encoding); break;
                    case "Payload":
                        string target = contract.Types.Keys.Single(k => k.EndsWith('/' + encoding, StringComparison.Ordinal));
                        item["$ref"] = Reference("type:" + target);
                        break;
                    default:
                        item["type"] = encoding switch
                        {
                            "int32" => JsonValue.Create("integer"),
                            "boolean" => JsonValue.Create("boolean"),
                            "string?" => Node(new[] { "string", "null" }),
                            _ => throw Incomplete("frames/" + name, "Unclassified position encoding " + encoding),
                        };
                        break;
                }
                positions.Add(item);
            }
            definitions.Add("frame:" + name, new JsonObject
            {
                ["type"] = "array", ["prefixItems"] = positions,
                ["minItems"] = layout.Length, ["maxItems"] = layout.Length,
                [Extension] = new JsonObject { ["direction"] = "response" },
            });
        }
        return new JsonObject
        {
            ["$schema"] = Dialect,
            ["$comment"] = "Contract catalogue. Select a $defs entry to validate a message shape. InfoCarrier metadata and codec probes provide additional compatibility checks.",
            ["$defs"] = definitions,
            [Extension] = new JsonObject
            {
                ["formatVersion"] = 2,
                ["protocolVersion"] = contract.Version,
                ["allocatedVersions"] = Node(contract.AllocatedVersions),
                ["inventory"] = Node(contract.Inventory),
            },
        };
    }

    internal static WireContract Read(JsonNode document)
    {
        // Preserve old references in memory. Never rewrite protected history or rebuild its source.
        if (document["FormatVersion"]?.GetValue<int>() == 1)
            return Value<WireContract>(document["Contract"]);
        if (document["Version"] is not null)
            return Value<WireContract>(document); // Earlier temporary command inputs.
        if (document["$schema"]?.GetValue<string>() != Dialect
            || document[Extension]?["formatVersion"]?.GetValue<int>() != 2)
            throw Incomplete("document", "Unsupported schema dialect or description format");

        var types = new Dictionary<string, TypeContract>(StringComparer.Ordinal);
        var frames = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var definitions = document["$defs"]?.AsObject() ?? throw Incomplete("$defs", "Missing definitions");
        foreach ((string name, JsonNode? definition) in definitions)
        {
            var shape = definition?.AsObject() ?? throw Incomplete(name, "Missing schema");
            var metadata = shape[Extension]?.AsObject() ?? throw Incomplete(name, "Missing coverage metadata");
            if (name.StartsWith("type:", StringComparison.Ordinal))
            {
                var required = Value<string[]>(shape["required"] ?? new JsonArray()).ToHashSet(StringComparer.Ordinal);
                var properties = new Dictionary<string, PropertyContract>(StringComparer.Ordinal);
                if (shape["properties"] is JsonObject fields)
                {
                    foreach ((string field, JsonNode? value) in fields)
                    {
                        var attributes = value?[Extension] ?? throw Incomplete(name + "/" + field, "Missing property metadata");
                        properties.Add(field, new PropertyContract(TypeReference(value!["$ref"]), required.Contains(field),
                            Value<bool>(attributes["readable"]), Value<bool>(attributes["writable"]),
                            Value<Dictionary<string, string>>(attributes["settings"])));
                    }
                }
                string kind = Value<string>(metadata["kind"]), encoding = Value<string>(metadata["encoding"]);
                var settings = Value<Dictionary<string, string>>(metadata["settings"]);
                var mappings = Value<Dictionary<string, string>>(metadata["enumAssignments"]);
                if (kind == "Object" && shape["anyOf"] is JsonArray variants)
                {
                    foreach (JsonNode? variant in variants.Where(v => v?["allOf"] is not null))
                    {
                        JsonArray pair = variant!["allOf"]!.AsArray();
                        mappings.Add("kind:" + pair[1]!["properties"]![settings["discriminatorProperty"]]!["const"]!.ToJsonString(),
                            TypeReference(pair[0]!["$ref"]));
                    }
                }
                string[] children = kind switch
                {
                    "Object" => properties.Values.Select(p => p.Type).Concat(mappings.Where(p => p.Key.StartsWith("kind:", StringComparison.Ordinal))
                        .Select(p => p.Value)).Distinct().Order().ToArray(),
                    "Enumerable" => [TypeReference(shape["items"]?["$ref"])],
                    _ when encoding == "nullable" => [TypeReference(shape["anyOf"]?[0]?["$ref"])],
                    _ => [],
                };
                types.Add(name[5..], new TypeContract(kind, encoding,
                    (WireDirection)Value<int>(metadata["direction"]), shape["additionalProperties"]?.GetValue<bool>() ?? true,
                    settings, properties, mappings, children));
            }
            else if (name.StartsWith("frame:", StringComparison.Ordinal))
            {
                frames.Add(name[6..], (shape["prefixItems"]?.AsArray() ?? throw Incomplete(name, "Missing positions"))
                    .Select(position => ReadPosition(position!, types: definitions)).ToArray());
            }
            else throw Incomplete(name, "Unclassified definition family");
        }
        JsonNode root = document[Extension]!;
        var contract = new WireContract(Value<int>(root["protocolVersion"]), Value<int[]>(root["allocatedVersions"]), types, frames,
            Value<Dictionary<string, string>>(root["inventory"]));
        foreach ((string path, TypeContract type) in types)
        {
            string? missing = type.Children.Concat(type.Properties.Values.Select(p => p.Type))
                .FirstOrDefault(child => !types.ContainsKey(child));
            if (missing is not null) throw Incomplete(path, "Missing referenced type " + missing);
        }

        // A bounded parser must not silently discard constraints it does not understand. This also
        // detects schema/annotation disagreement, dangling references, and edits to generated layouts.
        JsonObject expected = Write(contract);
        JsonNode actual = document.DeepClone();
        actual[Extension]!.AsObject().Remove("inspection");
        if (!JsonNode.DeepEquals(expected, actual))
            throw Incomplete(Difference(expected, actual), "Schema constraints or metadata differ from the supported representation");
        return contract;
    }

    private static JsonObject Shape(TypeContract type)
    {
        string numberHandling = type.Settings.GetValueOrDefault("numberHandling", "Strict");
        if (numberHandling is not ("Strict" or "AllowNamedFloatingPointLiterals")
            || type.Properties.Values.Any(p => p.Settings.GetValueOrDefault("numberHandling", "Strict") is not ("Strict" or "AllowNamedFloatingPointLiterals")))
            throw Incomplete(type.Encoding, "Non-default number representation requires explicit schema coverage");
        if (type.Encoding == "nullable")
            return new JsonObject { ["anyOf"] = new JsonArray
            {
                new JsonObject { ["$ref"] = Reference("type:" + type.Children.Single()) },
                new JsonObject { ["type"] = "null" },
            } };
        if (type.Kind == "Enumerable")
            return new JsonObject { ["type"] = Node(new[] { "array", "null" }),
                ["items"] = new JsonObject { ["$ref"] = Reference("type:" + type.Children.Single()) } };
        if (type.Kind == "Object")
            return new JsonObject { ["type"] = Node(new[] { "object", "null" }) };
        if (type.Encoding is "dynamic/registered-vocabulary" or "opaque/application-json")
            return new JsonObject();
        if (type.Encoding.StartsWith("enum/", StringComparison.Ordinal))
        {
            JsonNode[] values = type.Mappings.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => JsonNode.Parse(p.Value[(p.Value.IndexOf('|') + 1)..])!).ToArray();
            if (values.All(v => v.GetValueKind() == JsonValueKind.String))
                return new JsonObject { ["enum"] = Node(values.Select(v => v.GetValue<string>()).Distinct(StringComparer.Ordinal).ToArray()) };
            if (values.Any(v => v.GetValueKind() != JsonValueKind.Number))
                throw Incomplete(type.Encoding, "Mixed enum encoding requires explicit schema coverage");
            return new JsonObject { ["type"] = "integer" };
        }
        string scalar = type.Encoding.StartsWith("scalar/", StringComparison.Ordinal) ? type.Encoding[7..] : "";
        if (scalar is "System.Single" or "System.Double" && numberHandling == "AllowNamedFloatingPointLiterals")
            return new JsonObject { ["anyOf"] = new JsonArray
            {
                new JsonObject { ["type"] = "number" },
                new JsonObject { ["enum"] = Node(new[] { "NaN", "Infinity", "-Infinity" }) },
            } };
        JsonNode token = scalar switch
        {
            "System.String" or "System.Uri" or "base64" => Node(new[] { "string", "null" }),
            "System.Char" or "System.DateTime" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly"
                or "System.TimeSpan" or "System.Guid" => JsonValue.Create("string")!,
            "System.Boolean" => JsonValue.Create("boolean")!,
            "System.Single" or "System.Double" or "System.Decimal" => JsonValue.Create("number")!,
            "System.Byte" or "System.SByte" or "System.Int16" or "System.UInt16" or "System.Int32" or "System.UInt32"
                or "System.Int64" or "System.UInt64" => JsonValue.Create("integer")!,
            _ => throw Incomplete(type.Encoding, "Unclassified scalar shape"),
        };
        return new JsonObject { ["type"] = token };
    }

    private static string ReadPosition(JsonNode position, JsonObject types)
    {
        string field = Value<string>(position[Extension]?["position"]);
        if (field == "Tag") return field + ":" + position["const"]!.ToJsonString();
        if (field == "Payload")
        {
            string key = TypeReference(position["$ref"]);
            if (!types.ContainsKey("type:" + key)) throw Incomplete(key, "Missing frame payload definition");
            return field + ":" + key[(key.IndexOf('/') + 1)..];
        }
        string encoding = position["type"]?.ToJsonString() switch
        {
            "\"integer\"" => "int32", "\"boolean\"" => "boolean", "[\"string\",\"null\"]" => "string?",
            _ => throw Incomplete(field, "Unclassified frame position type"),
        };
        return field + ":" + encoding;
    }

    private static string Reference(string name) => "#/$defs/" + Uri.EscapeDataString(name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal));
    private static string Difference(JsonNode? expected, JsonNode? actual, string path = "document")
    {
        if (expected is JsonObject left && actual is JsonObject right)
        {
            foreach (string key in left.Select(p => p.Key).Union(right.Select(p => p.Key)).Order(StringComparer.Ordinal))
            {
                if (!left.ContainsKey(key) || !right.ContainsKey(key)) return path + "/" + key;
                if (!JsonNode.DeepEquals(left[key], right[key])) return Difference(left[key], right[key], path + "/" + key);
            }
        }
        else if (expected is JsonArray before && actual is JsonArray after)
        {
            if (before.Count != after.Count) return path + "/length";
            for (int index = 0; index < before.Count; index++)
                if (!JsonNode.DeepEquals(before[index], after[index])) return Difference(before[index], after[index], path + "/" + index);
        }
        return path;
    }

    private static string TypeReference(JsonNode? value)
    {
        string reference = Value<string>(value);
        const string prefix = "#/$defs/";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal)) throw Incomplete(reference, "Only local type references are supported");
        string name = Uri.UnescapeDataString(reference[prefix.Length..]).Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
        if (!name.StartsWith("type:", StringComparison.Ordinal)) throw Incomplete(reference, "Reference does not identify a type");
        return name[5..];
    }

    private static JsonNode Node<T>(T value) => JsonSerializer.SerializeToNode(value)!;
    private static T Value<T>(JsonNode? node) => node is null ? throw Incomplete("metadata", "Missing value") : node.Deserialize<T>()!;
    private static InvalidOperationException Incomplete(string path, string reason)
        => new($"Incomplete schema analysis at {path}: {reason}. Complete coverage or classification; increasing the protocol number does not resolve this.");
}
