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
    // The forward map holds wire ids, not nodes: identity has to survive serialization.
    private readonly Dictionary<object, int> _toIds = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<int, object?> _fromIds = [];
    private int _nextId;

    // Navigation-loaded probe, supplied only in Row mode (the server has the DbContext).
    private Func<object, INavigationBase, bool>? _isNavigationLoaded;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamicValueMapper" /> class.
    /// </summary>
    public DynamicValueMapper(IModel? model, TypeNodeMapper typeMapper, TypeNodeResolver typeResolver)
    {
        _model = model;
        _typeMapper = typeMapper;
        _typeResolver = typeResolver;
    }

    /// <summary>
    ///     Clears the per-message reference scope. **Must** be called at every message
    ///     boundary.
    /// </summary>
    /// <remarks>
    ///     Reference identity is a small integer that restarts at 1 for each message, and this
    ///     mapper is DI-scoped, so it outlives a single exchange. Without a reset, ids from an
    ///     earlier query collide with the current one and a lookup returns a stale object from
    ///     a previous result. (The predecessor maps were keyed by object/node reference, where
    ///     a collision was impossible and a stale entry was merely a leak — integer keys make
    ///     the reset load-bearing.)
    /// </remarks>
    public void ResetReferenceScope()
    {
        _toIds.Clear();
        _fromIds.Clear();
        _nextId = 0;
    }

    /// <summary>
    ///     Maps an entity <em>result row</em>: full scalar state plus loaded navigations, rather
    ///     than the key-only reference used for entities appearing inside a query tree
    ///     (see <c>docs/result-wire-format.md</c> §3.2).
    /// </summary>
    /// <param name="value">The entity instance.</param>
    /// <param name="type">Its declared type.</param>
    /// <param name="isNavigationLoaded">
    ///     Probe for whether EF actually loaded a navigation. Only the caller holds the
    ///     <c>DbContext</c> needed to answer this.
    /// </param>
    public DynamicValueNode ToRowValue(object? value, Type type, Func<object, INavigationBase, bool> isNavigationLoaded)
    {
        _isNavigationLoaded = isNavigationLoaded;
        try
        {
            return ToDynamicValue(value, type);
        }
        finally
        {
            _isNavigationLoaded = null;
        }
    }

    /// <inheritdoc />
    public DynamicValueNode ToDynamicValue(object? value, Type type)
    {
        if (value is not null && _toIds.TryGetValue(value, out int seenId))
        {
            // Back-reference on the wire — carries no data, so a cycle terminates.
            return new DynamicValueNode { Type = _typeMapper.ToTypeNode(type), Ref = seenId };
        }

        // Register BEFORE mapping members. Registering afterwards (the original order) only
        // survived because entities short-circuited to a key and never recursed; once a row
        // carries navigations, a cycle would recurse forever.
        int id = 0;
        if (value is not null && !IsPrimitive(value) && value is not Type)
        {
            id = ++_nextId;
            _toIds[value] = id;
        }

        return MapToNode(value, type, id);
    }

    private DynamicValueNode MapToNode(object? value, Type type, int id)
    {
        TypeNode typeNode = _typeMapper.ToTypeNode(type);

        // Entity. Two modes: a query-tree constant travels as identity only (research-findings
        // §7); a result row travels with its data.
        IEntityType? entityType = _model?.FindEntityType(type);
        if (entityType is not null && value is not null)
        {
            IReadOnlyList<object?> keyValues = entityType.FindPrimaryKey() is { } key
                ? key.Properties.Select(p => p.GetGetter().GetClrValue(value)).ToList()
                : [];
            var entityKey = new EntityKeyNode { EntityTypeName = entityType.Name, KeyValues = keyValues };

            if (_isNavigationLoaded is null)
            {
                return new DynamicValueNode { Id = id, Type = typeNode, EntityKey = entityKey };
            }

            return new DynamicValueNode
            {
                Id = id,
                Type = typeNode,
                EntityKey = entityKey,
                Properties = MapRowMembers(value, entityType),
            };
        }

        // Type value (typeof(X), the operand of a GetType() comparison, …). Must precede the
        // object-shape branch: walking a Type's public properties reflectively throws.
        if (value is Type typeValue)
        {
            return new DynamicValueNode { Type = typeNode, TypeValue = _typeMapper.ToTypeNode(typeValue) };
        }

        // Null: distinguishable from an absent value, and never referenceable.
        if (value is null)
        {
            return new DynamicValueNode { Type = typeNode, IsNull = true };
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

            return new DynamicValueNode { Id = id, Type = typeNode, Items = items };
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

        return new DynamicValueNode { Id = id, Type = typeNode, Properties = properties };
    }

    /// <summary>
    ///     Maps an entity row's members: every mapped scalar through its
    ///     <see cref="IProperty" /> accessor — so shadow properties and value converters are
    ///     honoured, which a public-reflection walk would miss (ADR-008 constraint 1) — plus
    ///     the navigations EF actually loaded.
    /// </summary>
    private List<DynamicPropertyValue> MapRowMembers(object value, IEntityType entityType)
    {
        var members = new List<DynamicPropertyValue>();

        foreach (IProperty property in entityType.GetProperties())
        {
            object? scalar = property.GetGetter().GetClrValue(value);
            members.Add(new DynamicPropertyValue
            {
                Name = property.Name,
                PrimitiveValue = IsPrimitive(scalar) ? PrimitiveCoercion.Normalize(scalar) : null,
                Value = IsPrimitive(scalar) ? null : ToDynamicValue(scalar, property.ClrType),
            });
        }

        foreach (INavigationBase navigation in entityType.GetNavigations().Cast<INavigationBase>()
                     .Concat(entityType.GetSkipNavigations()))
        {
            // Unloaded navigations are omitted entirely: shipping them would either force a
            // lazy load on the server or send a null that the client cannot tell apart from a
            // genuinely empty one.
            if (!_isNavigationLoaded!(value, navigation))
            {
                continue;
            }

            object? related = navigation.GetGetter().GetClrValue(value);
            members.Add(new DynamicPropertyValue
            {
                Name = navigation.Name,
                Value = ToDynamicValue(related, navigation.ClrType),
                IsLoadedNavigation = true,
            });
        }

        return members;
    }

    /// <inheritdoc />
    public object? FromDynamicValue(DynamicValueNode node)
    {
        // Back-reference: the target must already be registered. It is, because the forward
        // side only ever emits a Ref for a value it has already begun mapping, and this side
        // registers before populating members.
        if (node.Ref is int backReference)
        {
            return _fromIds.TryGetValue(backReference, out object? target)
                ? target
                : throw new InvalidOperationException(
                    $"Dangling wire reference {backReference}: no value with that id has been materialized.");
        }

        if (node.Id != 0 && _fromIds.TryGetValue(node.Id, out object? already))
        {
            return already;
        }

        object? value = Materialize(node);
        if (node.Id != 0)
        {
            _fromIds[node.Id] = value;
        }

        return value;
    }

    /// <summary>
    ///     Registers an externally-created instance under its wire id before its members are
    ///     populated, so a back-reference encountered mid-population resolves to the
    ///     partially-built instance instead of recursing.
    /// </summary>
    /// <remarks>
    ///     Entity rows are constructed by the client materializer, not here, because setting
    ///     shadow-state values requires the change tracker.
    /// </remarks>
    public void RegisterMaterialized(int id, object? value)
    {
        if (id != 0)
        {
            _fromIds[id] = value;
        }
    }

    private object? Materialize(DynamicValueNode node)
    {
        if (node.IsNull)
        {
            return null;
        }

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

            // Register before populating, so a member that points back at this instance
            // resolves to it instead of dangling (result-wire-format.md §3.1). Only possible on
            // this branch: the ctor branch must read its arguments before the instance exists,
            // which is safe because a ctor-only type cannot be mutated into a cycle.
            RegisterMaterialized(node.Id, instance);
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
