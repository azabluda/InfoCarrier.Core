// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     EF-metadata-driven <see cref="IDynamicValueMapper" /> (expression-serialization §3.1).
///     Entities map via <see cref="IModel" /> metadata (<see cref="IProperty" /> accessors and
///     entity-key references), never blind reflection; anonymous/record/DTO values map via
///     shape with ctor-param matching on the way back (aqua §2.3). Reference identity is
///     preserved per message via reference maps (aqua §2.3 — mandatory for EF identity
///     resolution and circular nav refs).
/// </summary>
public class DynamicValueMapper : IDynamicValueMapper
{
    private readonly IModel? _model;
    private readonly TypeNodeMapper _typeMapper;
    private readonly TypeNodeResolver _typeResolver;

    // Per-message reference maps (aqua ToContext/FromContext), ReferenceEqualityComparer-keyed.
    private readonly Dictionary<object, DynamicValueNode> _toContext = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<DynamicValueNode, object?> _fromContext = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamicValueMapper" /> class.
    /// </summary>
    public DynamicValueMapper(IModel? model, TypeNodeMapper typeMapper, TypeNodeResolver typeResolver)
    {
        _model = model;
        _typeMapper = typeMapper;
        _typeResolver = typeResolver;
    }

    /// <inheritdoc />
    public DynamicValueNode ToDynamicValue(object? value, Type type)
    {
        if (value is not null && _toContext.TryGetValue(value, out DynamicValueNode? existing))
        {
            return existing; // Reference preservation: same instance → same node.
        }

        DynamicValueNode node = MapToNode(value, type);
        if (value is not null)
        {
            _toContext[value] = node;
        }

        return node;
    }

    private DynamicValueNode MapToNode(object? value, Type type)
    {
        TypeNode typeNode = _typeMapper.ToTypeNode(type);

        // Entity: identify by EF entity-type name + key values (research-findings §7).
        IEntityType? entityType = _model?.FindEntityType(type);
        if (entityType is not null && value is not null)
        {
            IReadOnlyList<object?> keyValues = entityType.FindPrimaryKey() is { } key
                ? key.Properties.Select(p => p.GetGetter().GetClrValue(value)).ToList()
                : [];
            return new DynamicValueNode
            {
                Type = typeNode,
                EntityKey = new EntityKeyNode { EntityTypeName = entityType.Name, KeyValues = keyValues },
            };
        }

        // Type value (typeof(X), the operand of a GetType() comparison, …). Must precede the
        // object-shape branch: walking a Type's public properties reflectively throws.
        if (value is Type typeValue)
        {
            return new DynamicValueNode { Type = typeNode, TypeValue = _typeMapper.ToTypeNode(typeValue) };
        }

        // Scalar: a primitive standing where a dynamic value is required (typically a
        // collection element). Must precede the collection branch — string is IEnumerable —
        // and the object-shape branch, which cannot represent a primitive.
        if (IsPrimitive(value))
        {
            return new DynamicValueNode { Type = typeNode, PrimitiveValue = PrimitiveCoercion.Normalize(value) };
        }

        // Collection / array.
        if (value is IEnumerable enumerable && value is not string)
        {
            var items = new List<DynamicValueNode>();
            Type elementType = GetElementType(type);
            foreach (object? item in enumerable)
            {
                items.Add(ToDynamicValue(item, item?.GetType() ?? elementType));
            }

            return new DynamicValueNode { Type = typeNode, Items = items };
        }

        // Object shape: map public readable properties (records/anonymous/DTOs).
        var properties = new List<DynamicPropertyValue>();
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? propertyValue = value is null ? null : property.GetValue(value);
            properties.Add(new DynamicPropertyValue
            {
                Name = property.Name,
                PrimitiveValue = IsPrimitive(propertyValue) ? propertyValue : null,
                Value = IsPrimitive(propertyValue) ? null : ToDynamicValue(propertyValue, property.PropertyType),
            });
        }

        return new DynamicValueNode { Type = typeNode, Properties = properties };
    }

    /// <inheritdoc />
    public object? FromDynamicValue(DynamicValueNode node)
    {
        if (_fromContext.TryGetValue(node, out object? existing))
        {
            return existing; // Reference preservation on the way back.
        }

        object? value = Materialize(node);
        if (value is not null)
        {
            _fromContext[node] = value;
        }

        return value;
    }

    private object? Materialize(DynamicValueNode node)
    {
        Type type = _typeResolver.Resolve(node.Type);

        // Type value (mirrors the Type branch in MapToNode).
        if (node.TypeValue is not null)
        {
            return _typeResolver.Resolve(node.TypeValue);
        }

        // Scalar (mirrors the primitive branch in MapToNode).
        if (node.PrimitiveValue is not null)
        {
            return PrimitiveCoercion.Coerce(node.PrimitiveValue, type);
        }

        // Collection / array.
        if (node.Items is { } items)
        {
            Type elementType = GetElementType(type);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            foreach (DynamicValueNode item in items)
            {
                list.Add(FromDynamicValue(item));
            }

            if (type.IsArray)
            {
                Array array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }

        // Object shape: rehydrate via ctor-param matching (aqua §2.3) then settable properties.
        return RehydrateObject(node, type);
    }

    private object? RehydrateObject(DynamicValueNode node, Type type)
    {
        Dictionary<string, DynamicPropertyValue> byName = node.Properties
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        // Ctor-param matching: parameterless ctor wins, else match ctor params to properties
        // by name (OrdinalIgnoreCase) + assignable type (aqua §2.3).
        ConstructorInfo? ctor = type.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault(c => c.GetParameters().All(p => byName.ContainsKey(p.Name!)));

        object? instance;
        HashSet<string> ctorBound = new(StringComparer.OrdinalIgnoreCase);
        if (ctor is not null)
        {
            object?[] args = ctor.GetParameters()
                .Select(p =>
                {
                    ctorBound.Add(p.Name!);
                    return ReadValue(byName[p.Name!], p.ParameterType);
                })
                .ToArray();
            instance = ctor.Invoke(args);
        }
        else
        {
            instance = Activator.CreateInstance(type, nonPublic: true);
        }

        // Set remaining settable properties not bound by the ctor.
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || ctorBound.Contains(property.Name) || !byName.TryGetValue(property.Name, out DynamicPropertyValue? pv))
            {
                continue;
            }

            property.SetValue(instance, ReadValue(pv, property.PropertyType));
        }

        return instance;
    }

    /// <summary>
    ///     Reads one member value. <paramref name="targetType" /> is required because after a
    ///     serialization round-trip <see cref="DynamicPropertyValue.PrimitiveValue" /> arrives
    ///     as a <see cref="System.Text.Json.JsonElement" /> and must be converted back to the
    ///     member's declared type.
    /// </summary>
    private object? ReadValue(DynamicPropertyValue pv, Type targetType)
        => pv.Value is not null
            ? FromDynamicValue(pv.Value)
            : PrimitiveCoercion.Coerce(pv.PrimitiveValue, targetType);

    private static bool IsPrimitive(object? value)
        => value is null
            || value.GetType().IsPrimitive
            || value is string or decimal or DateTime or DateTimeOffset or TimeSpan or Guid or DateOnly or TimeOnly
            || value.GetType().IsEnum;

    private static Type GetElementType(Type type)
        => type.IsArray
            ? type.GetElementType()!
            : type.IsGenericType
                ? type.GetGenericArguments()[0]
                : typeof(object);
}
