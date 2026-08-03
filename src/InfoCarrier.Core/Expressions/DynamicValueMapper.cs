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

    // Row-mode probes, supplied only by the server, which alone holds the DbContext.
    private Func<object, INavigationBase, bool>? _isNavigationLoaded;
    private Func<object, bool>? _isTracked;
    private Func<object, IProperty, object?>? _readShadowValue;
    private Func<object, ISkipNavigation, bool, IEnumerable<object>>? _readJoinEntities;
    private Func<object, IEntityType?>? _findEntityType;

    /// <summary>
    ///     Routes an entity-keyed node to the client materializer, wherever in the graph it
    ///     appears. Set on the client only; <see langword="null" /> on the server.
    /// </summary>
    /// <remarks>
    ///     Entity instances cannot be built here: identity resolution and shadow state both
    ///     require the change tracker. Without this hook, an entity reached through a projection
    ///     member — <c>Select(c =&gt; new { c, o })</c> — fell through to
    ///     <see cref="RehydrateObject" /> and was reflection-constructed: detached, no identity
    ///     resolution, shadow properties lost, and (because the entity's parameterless
    ///     constructor took the ctor branch, which registered no wire id) every back-reference
    ///     from its loaded navigations dangled.
    /// </remarks>
    public Func<DynamicValueNode, object?>? EntityMaterializer { get; set; }

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
    ///     The type mapper this value mapper writes wire type identities with.
    /// </summary>
    public TypeNodeMapper TypeMapper => _typeMapper;

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
    /// <param name="isTracked">
    ///     Probe for whether the server's change tracker holds the instance. An entity built by
    ///     a projection is not tracked, and the client must not identity-resolve it.
    /// </param>
    /// <param name="readShadowValue">
    ///     Reads a shadow property, whose value lives in the entry rather than the object. A
    ///     TPH discriminator is one, and <c>GetGetter()</c> on it throws outright ("no backing
    ///     field could be found ... and the property does not have a getter") rather than
    ///     returning anything to skip.
    /// </param>
    /// <param name="findEntityType">
    ///     Names the entity type of an instance the CLR type cannot identify. A shared-type
    ///     entity is exactly that case: every many-to-many join entity is a
    ///     <c>Dictionary&lt;string, object&gt;</c>, and several of them are that same type.
    /// </param>
    public DynamicValueNode ToRowValue(
        object? value,
        Type type,
        Func<object, INavigationBase, bool> isNavigationLoaded,
        Func<object, bool> isTracked,
        Func<object, IProperty, object?> readShadowValue,
        Func<object, ISkipNavigation, bool, IEnumerable<object>> readJoinEntities,
        Func<object, IEntityType?> findEntityType)
    {
        _isNavigationLoaded = isNavigationLoaded;
        _isTracked = isTracked;
        _readShadowValue = readShadowValue;
        _readJoinEntities = readJoinEntities;
        _findEntityType = findEntityType;
        try
        {
            return ToDynamicValue(value, type);
        }
        finally
        {
            _isNavigationLoaded = null;
            _isTracked = null;
            _readShadowValue = null;
            _readJoinEntities = null;
            _findEntityType = null;
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
        // Entity. Two modes: a query-tree constant travels as identity only (research-findings
        // §7); a result row travels with its data.
        // `FindRuntimeEntityType`, not `FindEntityType`: the CLR type in hand may be a *proxy*,
        // which is not in the model and is not a name the client could resolve even if it were.
        // A materialization interceptor produces one — EF's own `F1MaterializationInterceptor`
        // returns `Driver+DriverProxy` — and a collection maps each item by `item.GetType()`, so
        // the proxy arrived as the declared type and every row of the F1 model was refused by the
        // deserialization allowlist (ADR-008). EF walks base types for the same reason.
        IEntityType? entityType = _model?.FindRuntimeEntityType(type);

        // No CLR type can identify a *shared-type* entity: every many-to-many join entity is a
        // `Dictionary<string, object>` and several of them are that same type, told apart only
        // by name. So the lookup above returns null and the value used to fall through to the
        // collection branch — a `Dictionary` is enumerable, and rebuilding it as one produced
        // "the value [OneId, 1] is not of type System.String". Ask what actually holds the
        // instance instead: on the server every value mapped here came out of the change
        // tracker, and the tracker knows which shared type this one is.
        entityType ??= value is not null && _findEntityType is not null
            ? _findEntityType(value)
            : null;

        // A row is mapped by its runtime type, but a *navigation* is mapped by the type the
        // navigation declares — and in a TPH hierarchy those differ. `Root.OptionalSingle` is
        // declared `OptionalSingle1`, so a loaded `OptionalSingle1Derived` travelled named as its
        // own base and the client dutifully materialized the base: "Unable to cast object of type
        // 'OptionalSingle2' to type 'OptionalSingle2Derived'". The instance knows what it is.
        if (entityType is not null
            && value is not null
            && value.GetType() != type
            && _model!.FindRuntimeEntityType(value.GetType()) is { } runtimeEntityType
            && entityType.IsAssignableFrom(runtimeEntityType))
        {
            entityType = runtimeEntityType;
        }

        if (entityType is not null)
        {
            // What the entity *is* in the model, which is what the client can name. This is what
            // strips a proxy back to the type it stands for, and what carries a TPH row as its
            // own derived type rather than the one its navigation declares.
            type = entityType.ClrType;
        }

        TypeNode typeNode = _typeMapper.ToTypeNode(type);

        if (entityType is not null && value is not null)
        {
            IReadOnlyList<object?> keyValues = entityType.FindPrimaryKey() is { } key
                ? key.Properties.Select(p => PrimitiveCoercion.ToWireValue(p, ReadProperty(value, p))).ToList()
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
                IsTracked = _isTracked!(value),
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

        // Object shape: public readable properties *and* public fields (records/anonymous/DTOs
        // and plain structs). Fields are not an edge case — a struct written
        // `public int X, Y;` has no properties at all, and mapping only properties round-tripped
        // it as `default`.
        var properties = new List<DynamicPropertyValue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddMember(string name, Type memberType, object? memberValue)
        {
            if (!seen.Add(name))
            {
                return;
            }

            // The compact `PrimitiveValue` form carries no type of its own, so the reverse path
            // can only rebuild it from the member's *declared* type. That is enough when the two
            // agree, and wrong when they do not: a member declared `object` holding a decimal
            // came back as the raw JsonElement, which prints as the right number and compares
            // equal to nothing. Such a value goes as a full node instead, which carries its own
            // TypeNode — but only when it is a primitive. Widening the *node* type to the
            // runtime type generally would make the mapper walk graphs the declared type used to
            // truncate (an `object` member holding a DbContext), and that stack-overflows.
            Type declared = Nullable.GetUnderlyingType(memberType) ?? memberType;
            bool isPrimitive = IsPrimitive(memberValue);
            bool asPrimitive = isPrimitive
                && (memberValue is null || declared == memberValue.GetType());

            properties.Add(new DynamicPropertyValue
            {
                Name = name,

                // Normalize, as MapRowMembers does. An enum carries its own concrete runtime
                // type, which can never be pre-registered with the source-generated serializer
                // (ADR-008 constraint 8), so an un-normalized one fails at write time —
                // AutoTransactionBehavior on a captured DbContext did exactly that.
                PrimitiveValue = asPrimitive ? PrimitiveCoercion.Normalize(memberValue) : null,
                Value = asPrimitive
                    ? null
                    : ToDynamicValue(memberValue, isPrimitive ? memberValue!.GetType() : memberType),
            });
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.GetIndexParameters().Length == 0)
            {
                AddMember(property.Name, property.PropertyType, value is null ? null : property.GetValue(value));
            }
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            AddMember(field.Name, field.FieldType, value is null ? null : field.GetValue(value));
        }

        return new DynamicValueNode { Id = id, Type = typeNode, Properties = properties };
    }

    /// <summary>
    ///     Reads one mapped scalar off an entity.
    /// </summary>
    /// <remarks>
    ///     A shadow property has no CLR member to read, so its value comes from the entry —
    ///     which only the server can reach. Where no reader was supplied (the client, and query
    ///     trees rather than result rows) this falls through to the accessor and lets EF raise
    ///     its own diagnosis rather than inventing a value.
    /// </remarks>
    private object? ReadProperty(object value, IProperty property)
        => property.IsShadowProperty() && _readShadowValue is not null
            ? _readShadowValue(value, property)
            : property.GetGetter().GetClrValue(value);

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
            // The *provider* value, so a value converter is honoured rather than merely gone
            // through (`PrimitiveCoercion.ToWireValue`). Sending the CLR value instead left the
            // reflective member walk to decompose whatever the converter was hiding, and
            // `IPAddress.ScopeId` throws `SocketException` for an IPv4 address.
            object? scalar = PrimitiveCoercion.ToWireValue(property, ReadProperty(value, property));
            members.Add(new DynamicPropertyValue
            {
                Name = property.Name,
                PrimitiveValue = IsPrimitive(scalar) ? scalar : null,
                Value = IsPrimitive(scalar) ? null : ToDynamicValue(scalar, PrimitiveCoercion.WireType(property)),
            });
        }

        foreach (INavigationBase navigation in entityType.GetNavigations().Cast<INavigationBase>()
                     .Concat(entityType.GetSkipNavigations()))
        {
            // Unloaded navigations send no *value*: shipping one would either force a lazy load
            // on the server or send a null the client cannot tell apart from a genuinely empty
            // collection. Its join rows are a separate question, handled below — this used to
            // `continue` here and that is what made explicit skip-navigation loading return
            // nothing (plan A5/A6).
            bool loaded = _isNavigationLoaded!(value, navigation);

            // A shadow navigation has no CLR member to read, and `GetGetter` on one does not
            // return null — it throws: "No backing field could be found for property
            // 'UnidirectionalEntityTwo.UnidirectionalEntityThree' and the property does not have
            // a getter". A *unidirectional* many-to-many is where these come from: EF still
            // declares the inverse skip navigation in the model, with no property behind it.
            // There is nothing to send and nowhere on the client to put it, but the join rows
            // below are still the payload the navigation exists to carry, so the walk continues
            // rather than skipping the navigation outright.
            if (loaded)
            {
                // A shadow navigation has no CLR member to read — `GetGetter` on one throws
                // rather than returning null (L18) — so it travels as a *value-less* loaded
                // member. That is not nothing: "loaded" is real state the client cannot
                // otherwise learn, and `Load_collection_already_loaded_untyped_*` asserts it on
                // exactly this kind of navigation. What populates it is the join rows below.
                members.Add(new DynamicPropertyValue
                {
                    Name = navigation.Name,
                    Value = navigation.IsShadowProperty()
                        ? null
                        : ToDynamicValue(navigation.GetGetter().GetClrValue(value), navigation.ClrType),
                    IsLoadedNavigation = true,
                });
            }

            // A skip navigation's join rows travel whether or not the navigation itself is
            // *loaded*, which is the whole point: they are what connects the two sides, and the
            // client cannot rebuild them — a payload and a join table's extra foreign keys exist
            // only here.
            //
            // Not gated on `loaded`, because EF deliberately leaves a **filtered** include
            // unloaded and that is exactly the shape explicit skip loading uses:
            // `ManyToManyLoader.Query` builds
            // `…SelectMany(e => e.TwoSkip).NotQuiteInclude(e => e.OneSkip.Where(…))`, and
            // `ManyToManyLoadTestBase.Load_collection` asserts that `OneSkip` comes back *not*
            // loaded. While this sat behind the loaded check the join rows were never sent, so
            // the rows arrived and nothing joined them: `left.TwoSkip` was empty in all 24
            // parameterizations (plan A5).
            //
            // Shared-type join entities travel too, since L20: they are named by the change
            // tracker rather than by their `Dictionary<string, object>` CLR type, so they decode
            // as rows like any other.
            if (navigation is ISkipNavigation skip && _readJoinEntities is not null)
            {
                List<object> joinRows = [.. _readJoinEntities(value, skip, loaded)];
                if (joinRows.Count > 0)
                {
                    members.Add(new DynamicPropertyValue
                    {
                        Name = navigation.Name,
                        Value = ToDynamicValue(joinRows, typeof(List<object>)),
                        IsJoinRows = true,
                    });
                }
            }
        }

        return members;
    }

    /// <inheritdoc />
    /// <summary>
    ///     Reads one wire property value back, whichever of the two forms it took.
    /// </summary>
    /// <remarks>
    ///     A <see cref="DynamicPropertyValue" /> carries either a primitive directly or a nested
    ///     node; callers that only ever see one of the two would silently read null for the other.
    /// </remarks>
    public object? FromPropertyValue(DynamicPropertyValue property, Type declaredType)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property.Value is { } nested
            ? FromDynamicValue(nested)
            : PrimitiveCoercion.Coerce(property.PrimitiveValue, declaredType);
    }

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

    /// <summary>
    ///     Rehydrates a node as a plain object shape, bypassing <see cref="EntityMaterializer" />.
    /// </summary>
    /// <remarks>
    ///     For an entity-keyed node whose entity type the client model does not know. Routing it
    ///     back through <see cref="FromDynamicValue" /> would re-enter the entity hook that just
    ///     gave up on it, and recurse forever.
    /// </remarks>
    public object? FromShape(DynamicValueNode node)
    {
        object? value = RehydrateObject(node, _typeResolver.Resolve(node.Type));
        RegisterMaterialized(node.Id, value);
        return value;
    }

    private object? Materialize(DynamicValueNode node)
    {
        if (node.IsNull)
        {
            return null;
        }

        // Entity, anywhere in the graph. Must precede the object-shape branch.
        if (node.EntityKey is not null)
        {
            // Client: the materializer owns identity resolution and shadow state.
            if (EntityMaterializer is { } materializeEntity)
            {
                return materializeEntity(node);
            }

            // Server: an entity inside a query tree travels as identity only, so the
            // object-shape branch would rebuild an empty shell — which is what made
            // `Contains(customer)` match nothing. Restore the key it does carry.
            if (_model?.FindEntityType(node.EntityKey.EntityTypeName) is { } entityType)
            {
                return MaterializeEntityReference(node, entityType);
            }
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

            // A `List<T>` does not satisfy every declared collection type. `Contains` over a
            // local `ReadOnlyCollection<T>` puts one in a constant of that exact type, and
            // `Expression.Constant` rejects the mismatch outright ("Argument types do not
            // match"). Rebuild the declared type when it can wrap a list.
            return type.IsAssignableFrom(list.GetType())
                ? list
                : ConstructCollection(type, elementType, list) ?? list;
        }

        // Object shape: rehydrate via ctor-param matching (aqua §2.3) then settable properties.
        return RehydrateObject(node, type);
    }

    /// <summary>
    ///     Rebuilds an entity that travelled as a <em>reference</em> — an
    ///     <see cref="EntityKeyNode" /> and nothing else — with its primary key populated.
    /// </summary>
    /// <remarks>
    ///     That key is the whole point of the reference form: EF rewrites
    ///     <c>Contains(entity)</c> and <c>entity1 == entity2</c> into comparisons over key
    ///     values, so an instance without them silently matches nothing rather than failing.
    /// </remarks>
    private object MaterializeEntityReference(DynamicValueNode node, IEntityType entityType)
    {
        object instance = Activator.CreateInstance(entityType.ClrType, nonPublic: true)!;
        RegisterMaterialized(node.Id, instance);

        IKey? primaryKey = entityType.FindPrimaryKey();
        if (primaryKey is null || node.EntityKey!.KeyValues.Count != primaryKey.Properties.Count)
        {
            return instance;
        }

        for (int i = 0; i < primaryKey.Properties.Count; i++)
        {
            IProperty property = primaryKey.Properties[i];
            if (property.PropertyInfo is { CanWrite: true } clrProperty)
            {
                clrProperty.SetValue(
                    instance, PrimitiveCoercion.FromWireValue(property, node.EntityKey.KeyValues[i]));
            }
        }

        return instance;
    }

    private object? RehydrateObject(DynamicValueNode node, Type type)
    {
        // Distinct: the forward path emits properties and fields, and a name could in
        // principle be claimed by both.
        Dictionary<string, DynamicPropertyValue> byName = node.Properties
            .DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        // Ctor-param matching: parameterless ctor wins, else match ctor params to properties
        // by name (OrdinalIgnoreCase) + assignable type (aqua §2.3).
        ConstructorInfo? ctor = type.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault(c => c.GetParameters().All(p => byName.ContainsKey(p.Name!)));

        object? instance;
        HashSet<string> ctorBound = new(StringComparer.OrdinalIgnoreCase);
        if (ctor is { } parameterized && parameterized.GetParameters().Length > 0)
        {
            // The only ordering that cannot register first: the arguments have to be read
            // before the instance exists. Safe, because a type reachable only through its
            // constructor cannot be mutated into a cycle pointing back at itself.
            object?[] args = parameterized.GetParameters()
                .Select(p =>
                {
                    ctorBound.Add(p.Name!);
                    return ReadValue(byName[p.Name!], p.ParameterType);
                })
                .ToArray();
            instance = parameterized.Invoke(args);
        }
        else
        {
            instance = ctor is not null
                ? ctor.Invoke([])
                : Activator.CreateInstance(type, nonPublic: true);
        }

        // Register before populating members, so one that points back at this instance resolves
        // to it instead of dangling (result-wire-format.md §3.1). The parameterless-ctor case
        // used to fall in the branch above and skip this entirely.
        RegisterMaterialized(node.Id, instance);

        // Set remaining settable properties not bound by the ctor.
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || ctorBound.Contains(property.Name) || !byName.TryGetValue(property.Name, out DynamicPropertyValue? pv))
            {
                continue;
            }

            property.SetValue(instance, ReadValue(pv, property.PropertyType));
        }

        // …and public fields, which the forward path also maps. Writing to a boxed struct
        // through reflection mutates the box, which is the instance about to be returned.
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.IsInitOnly || ctorBound.Contains(field.Name) || !byName.TryGetValue(field.Name, out DynamicPropertyValue? fv))
            {
                continue;
            }

            field.SetValue(instance, ReadValue(fv, field.FieldType));
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

    /// <summary>
    ///     Rebuilds <paramref name="items" /> as <paramref name="type" /> via a single-argument
    ///     constructor that accepts a list — the shape of <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}" />,
    ///     <see cref="System.Collections.ObjectModel.Collection{T}" />, <see cref="HashSet{T}" />
    ///     and friends. Returns <see langword="null" /> when the type offers no such way in,
    ///     which is the case for interfaces like <c>IOrderedEnumerable&lt;T&gt;</c>; the caller
    ///     then keeps the list.
    /// </summary>
    private static object? ConstructCollection(Type type, Type elementType, IList items)
    {
        if (!type.IsInterface && !type.IsAbstract)
        {
            foreach (ConstructorInfo ctor in type.GetConstructors())
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length != 1 || !parameters[0].ParameterType.IsInstanceOfType(items))
                {
                    continue;
                }

                try
                {
                    return ctor.Invoke([items]);
                }
                catch (TargetInvocationException)
                {
                    break;
                }
            }
        }

        // A set interface — `ISet<T>`, `IReadOnlySet<T>` — is satisfied by a HashSet, without
        // hunting for a factory. (The sequence interfaces never reach here: a list already
        // satisfies them.)
        Type hashSet = typeof(HashSet<>).MakeGenericType(elementType);
        if (type.IsAssignableFrom(hashSet))
        {
            return Activator.CreateInstance(hashSet, items);
        }

        return CreateRange(type, elementType, items);
    }

    /// <summary>
    ///     Builds <paramref name="type" /> through a static <c>CreateRange</c> factory.
    /// </summary>
    /// <remarks>
    ///     The immutable collections have no public constructor at all — an
    ///     <c>ImmutableHashSet&lt;T&gt;</c> is only reachable through
    ///     <c>ImmutableHashSet.CreateRange</c> on a companion static class — and neither the
    ///     concrete types nor <c>IImmutableSet&lt;T&gt;</c> can be produced any other way. Only
    ///     the declaring assembly is scanned, and never the core library, whose collection
    ///     interfaces are all handled above.
    /// </remarks>
    private static object? CreateRange(Type type, Type elementType, IList items)
    {
        if (type.Assembly == typeof(object).Assembly)
        {
            return null;
        }

        Type enumerable = typeof(IEnumerable<>).MakeGenericType(elementType);

        // Ordered by name so the choice between equally-valid factories is deterministic.
        foreach (Type candidate in type.Assembly.GetExportedTypes()
                     .Where(t => t is { IsAbstract: true, IsSealed: true, IsGenericTypeDefinition: false })
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            foreach (MethodInfo method in candidate.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "CreateRange"
                    || !method.IsGenericMethodDefinition
                    || method.GetGenericArguments().Length != 1
                    || method.GetParameters().Length != 1)
                {
                    continue;
                }

                MethodInfo closed;
                try
                {
                    closed = method.MakeGenericMethod(elementType);
                }
                catch (ArgumentException)
                {
                    // Generic constraints not satisfied by this element type.
                    continue;
                }

                if (closed.GetParameters()[0].ParameterType.IsAssignableFrom(enumerable)
                    && type.IsAssignableFrom(closed.ReturnType))
                {
                    return closed.Invoke(null, [items]);
                }
            }
        }

        return null;
    }

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
