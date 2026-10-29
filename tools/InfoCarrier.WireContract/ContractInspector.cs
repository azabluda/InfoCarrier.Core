// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.WireContract;

/// <summary>Reads the effective metadata used by the shipped default serializers.</summary>
internal static class ContractInspector
{
    internal static WireContract Inspect(int[] allocatedVersions, Dictionary<string, string[]> frames)
    {
        var assemblies = new[]
        {
            typeof(InfoCarrierJsonContext).Assembly,
            Assembly.Load("InfoCarrier.Core.Abstractions"),
            Assembly.Load("InfoCarrier.Core.AspNetCore"),
        };
        var contexts = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && typeof(JsonSerializerContext).IsAssignableFrom(t))
            .ToDictionary(t => t, t => (JsonSerializerContext)(t.GetProperty("Default")?.GetValue(null)
                ?? throw Incomplete(t.ToString(), "A discovered context has no Default instance.")));
        if (contexts.Count == 0)
        {
            throw Incomplete("contexts", "No registered serialization contexts were found.");
        }

        Dictionary<string, string> inventory = Coverage.Inventory();
        var registered = new Dictionary<string, (Type Context, JsonTypeInfo Info)>(StringComparer.Ordinal);
        foreach ((Type contextType, JsonSerializerContext context) in contexts)
        {
            inventory.Add("context:" + contextType, context.GetType().Assembly.GetName().Name!);
            foreach (PropertyInfo property in contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(JsonTypeInfo).IsAssignableFrom(p.PropertyType)))
            {
                var info = (JsonTypeInfo)property.GetValue(context)!;
                registered.Add(Key(contextType, info.Type), (contextType, info));
            }
        }

        foreach (Type serializer in assemblies.SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && typeof(IInfoCarrierSerializer).IsAssignableFrom(t)))
        {
            inventory.Add("serializer:" + serializer, serializer.Assembly.GetName().Name!);
        }

        var directions = new Dictionary<string, WireDirection>(StringComparer.Ordinal);
        foreach ((Type context, Type root, WireDirection direction) in Coverage.Roots())
        {
            Walk(context, root, direction);
        }
        string[] uncovered = registered.Keys.Except(directions.Keys).Order().ToArray();
        if (uncovered.Length != 0)
        {
            throw Incomplete(string.Join(", ", uncovered), "Registered types have no covered request or response path.");
        }
        var types = registered.OrderBy(p => p.Key).ToDictionary(p => p.Key,
            p => InspectType(p.Value.Info, directions[p.Key], t => Key(p.Value.Context, t)), StringComparer.Ordinal);
        inventory.Add("runtime:System.Text.Json", typeof(JsonSerializer).Assembly.GetName().Version!.ToString());
        return new WireContract(InfoCarrierEnvelope.CurrentProtocolVersion, allocatedVersions, types, frames, inventory);

        void Walk(Type context, Type type, WireDirection direction)
        {
            string key = Key(context, type);
            // Faults are carried only in responses, despite being an envelope property.
            if (type == typeof(InfoCarrierFault))
            {
                direction = WireDirection.Response;
            }
            WireDirection previous = directions.GetValueOrDefault(key);
            if ((previous & direction) == direction)
            {
                return;
            }
            if (!registered.TryGetValue(key, out var registration))
            {
                throw Incomplete(key, "A covered type has no registered source-generated metadata.");
            }
            directions[key] = previous | direction;
            foreach (Type child in ChildTypes(registration.Info))
            {
                Walk(context, child, direction);
            }
            if (type == typeof(object) && context == typeof(ExpressionJsonContext))
            {
                foreach (Type primitive in Coverage.PrimitiveValues)
                {
                    Walk(context, primitive, direction);
                }
            }
        }
    }

    internal static TypeContract InspectType(JsonTypeInfo info, WireDirection direction, Func<Type, string> key)
    {
        CheckConverter(info.Converter, key(info.Type));
        if (info.OnDeserializing is not null || info.OnDeserialized is not null
            || info.OnSerializing is not null || info.OnSerialized is not null)
        {
            throw Incomplete(key(info.Type), "Serialization callbacks require explicit coverage.");
        }
        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["converter"] = ConverterName(info.Converter),
            ["numberHandling"] = (info.NumberHandling ?? info.Options.NumberHandling).ToString(),
            ["defaultIgnore"] = info.Options.DefaultIgnoreCondition.ToString(),
            ["requiredConstructor"] = info.Options.RespectRequiredConstructorParameters.ToString(),
            ["nullableAnnotations"] = info.Options.RespectNullableAnnotations.ToString(),
            ["caseInsensitive"] = info.Options.PropertyNameCaseInsensitive.ToString(),
            ["referenceHandling"] = info.Options.ReferenceHandler?.GetType().ToString() ?? "none",
            ["unknownTypeHandling"] = info.Options.UnknownTypeHandling.ToString(),
            ["objectCreationHandling"] = info.Options.PreferredObjectCreationHandling.ToString(),
            ["outOfOrderMetadata"] = info.Options.AllowOutOfOrderMetadataProperties.ToString(),
            ["dictionaryKeyPolicy"] = info.Options.DictionaryKeyPolicy?.GetType().ToString() ?? "none",
            ["maxDepth"] = (info.Options.MaxDepth == 0 ? 64 : info.Options.MaxDepth).ToString(CultureInfo.InvariantCulture),
        };
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (info.Type.IsEnum)
        {
            foreach (string name in Enum.GetNames(info.Type))
            {
                object value = Enum.Parse(info.Type, name);
                object number = Convert.ChangeType(value, Enum.GetUnderlyingType(info.Type), CultureInfo.InvariantCulture);
                mappings.Add("enum:" + name, Convert.ToString(number, CultureInfo.InvariantCulture)
                    + "|" + JsonSerializer.Serialize(value, info));
            }
        }
        if (info.PolymorphismOptions is { } polymorphism)
        {
            settings.Add("discriminatorProperty", polymorphism.TypeDiscriminatorPropertyName);
            settings.Add("ignoreUnknownDiscriminator", polymorphism.IgnoreUnrecognizedTypeDiscriminators.ToString());
            settings.Add("unknownDerivedHandling", polymorphism.UnknownDerivedTypeHandling.ToString());
            foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
            {
                if (derived.TypeDiscriminator is null)
                {
                    throw Incomplete(key(info.Type), "An untagged polymorphic variant requires classification.");
                }
                mappings.Add("kind:" + JsonSerializer.Serialize(derived.TypeDiscriminator), key(derived.DerivedType));
            }
        }
        var properties = new Dictionary<string, PropertyContract>(StringComparer.Ordinal);
        foreach (JsonPropertyInfo property in info.Properties.Where(p => p.Get is not null || p.Set is not null))
        {
            if (property.CustomConverter is { } converter)
            {
                CheckConverter(converter, key(info.Type) + "/" + property.Name);
            }
            if (property.IsExtensionData)
            {
                throw Incomplete(key(info.Type) + "/" + property.Name, "Extension data needs an explicit contract.");
            }
            var propertySettings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["converter"] = property.CustomConverter is { } custom ? ConverterName(custom) : "default",
                ["numberHandling"] = (property.NumberHandling ?? info.NumberHandling ?? info.Options.NumberHandling).ToString(),
                ["objectCreationHandling"] = property.ObjectCreationHandling?.ToString() ?? "default",
                ["getNullable"] = property.IsGetNullable.ToString(),
                ["setNullable"] = property.IsSetNullable.ToString(),
                ["conditionalWrite"] = (property.ShouldSerialize is not null).ToString(),
            };
            bool required = property.IsRequired || info.Options.RespectRequiredConstructorParameters
                && property.AssociatedParameter is { IsMemberInitializer: false, HasDefaultValue: false };
            properties.Add(property.Name, new PropertyContract(key(property.PropertyType), required,
                property.Set is not null || property.AssociatedParameter is not null, property.Get is not null,
                propertySettings));
        }
        return new TypeContract(info.Kind.ToString(), Encoding(info.Type, info.Kind), direction,
            (info.UnmappedMemberHandling ?? info.Options.UnmappedMemberHandling) == JsonUnmappedMemberHandling.Skip,
            settings, properties, mappings, ChildTypes(info).Select(key).Distinct().Order().ToArray());
    }

    private static IEnumerable<Type> ChildTypes(JsonTypeInfo info)
    {
        foreach (JsonPropertyInfo property in info.Properties.Where(p => p.Get is not null || p.Set is not null))
        {
            yield return property.PropertyType;
        }
        if (info.PolymorphismOptions is { } polymorphism)
        {
            foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
            {
                yield return derived.DerivedType;
            }
        }
        if (Nullable.GetUnderlyingType(info.Type) is { } underlying)
        {
            yield return underlying;
        }
        else if (info.Kind == JsonTypeInfoKind.Enumerable)
        {
            Type sequence = info.Type.IsArray ? info.Type.GetElementType()!
                : info.Type.GetInterfaces().Prepend(info.Type)
                    .Single(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)).GetGenericArguments()[0];
            yield return sequence;
        }
        else if (info.Kind == JsonTypeInfoKind.Dictionary)
        {
            throw Incomplete(info.Type.ToString(), "Dictionary contracts require explicit key and value coverage.");
        }
    }

    private static string Encoding(Type type, JsonTypeInfoKind kind)
    {
        if (type == typeof(byte[]))
        {
            return "scalar/base64";
        }
        if (type.IsEnum)
        {
            return "enum/" + Enum.GetUnderlyingType(type);
        }
        if (Nullable.GetUnderlyingType(type) is not null)
        {
            return "nullable";
        }
        if (type == typeof(object))
        {
            return "dynamic/registered-vocabulary";
        }
        if (type == typeof(JsonElement))
        {
            return "opaque/application-json";
        }
        if (kind == JsonTypeInfoKind.None)
        {
            if (!Coverage.PrimitiveValues.Contains(type))
            {
                throw Incomplete(type.ToString(), "An unfamiliar scalar encoding requires classification.");
            }
            return "scalar/" + type;
        }
        return kind.ToString();
    }

    private static void CheckConverter(JsonConverter converter, string path)
    {
        if (converter.GetType().Assembly != typeof(JsonSerializer).Assembly)
        {
            throw Incomplete(path, "A custom converter requires explicit structural coverage.");
        }
    }

    private static string ConverterName(JsonConverter converter)
    {
        Type type = converter.GetType();
        return (type.IsGenericType ? type.GetGenericTypeDefinition() : type).ToString();
    }

    private static string Key(Type context, Type type) => context + "/" + type;

    private static InvalidOperationException Incomplete(string path, string reason)
        => new($"Incomplete wire analysis at {path}: {reason} Complete coverage before classifying compatibility.");
}
