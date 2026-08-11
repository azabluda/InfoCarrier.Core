// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Reflection;
using InfoCarrier.Core.Query;
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
    private readonly IReadOnlyList<ValueMapping.IInfoCarrierValueMapper> _valueMappers;

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
    public virtual Func<DynamicValueNode, object?>? EntityMaterializer { get; set; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamicValueMapper" /> class.
    /// </summary>
    /// <param name="model">The EF model, or <see langword="null" /> when none is available.</param>
    /// <param name="typeMapper">Writes wire type identities.</param>
    /// <param name="typeResolver">Resolves them back, through the ADR-008 allowlist.</param>
    /// <param name="valueMappers">
    ///     The application's <see cref="ValueMapping.IInfoCarrierValueMapper" /> chain, in
    ///     registration order — first claim wins. Empty by default, and an empty chain is
    ///     exactly today's behaviour: the hooks below are two loops that do not run.
    /// </param>
    public DynamicValueMapper(
        IModel? model,
        TypeNodeMapper typeMapper,
        TypeNodeResolver typeResolver,
        IEnumerable<ValueMapping.IInfoCarrierValueMapper>? valueMappers = null)
    {
        _model = model;
        _typeMapper = typeMapper;
        _typeResolver = typeResolver;
        _valueMappers = valueMappers as IReadOnlyList<ValueMapping.IInfoCarrierValueMapper>
            ?? valueMappers?.ToList()
            ?? [];
    }

    /// <summary>
    ///     The type mapper this value mapper writes wire type identities with.
    /// </summary>
    public virtual TypeNodeMapper TypeMapper => _typeMapper;

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
    public virtual void ResetReferenceScope()
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
    public virtual DynamicValueNode ToRowValue(
        object? value,
        Type type,
        Func<object, INavigationBase, bool> isNavigationLoaded,
        Func<object, bool> isTracked,
        Func<object, IProperty, object?> readShadowValue,
        Func<object, ISkipNavigation, bool, IEnumerable<object>> readJoinEntities,
        Func<object, IEntityType?> findEntityType)
        => ToRowValue(value, type, isNavigationLoaded, isTracked, readShadowValue, readJoinEntities, findEntityType, shape: null);

    /// <summary>
    ///     Maps an entity result row whose entity types the <em>query</em> named.
    /// </summary>
    /// <remarks>
    ///     A row projected straight out of a navigation — <c>Select(p =&gt; p.PersonAddress)</c> —
    ///     is an owned entity with no CLR-type name, no tracker entry to be found through, and no
    ///     navigation in hand at this point to ask. The query is what knows; see
    ///     <see cref="ProjectionShape" /> (A55/A56).
    /// </remarks>
    internal DynamicValueNode ToRowValue(
        object? value,
        Type type,
        Func<object, INavigationBase, bool> isNavigationLoaded,
        Func<object, bool> isTracked,
        Func<object, IProperty, object?> readShadowValue,
        Func<object, ISkipNavigation, bool, IEnumerable<object>> readJoinEntities,
        Func<object, IEntityType?> findEntityType,
        ProjectionShape? shape)
    {
        _isNavigationLoaded = isNavigationLoaded;
        _isTracked = isTracked;
        _readShadowValue = readShadowValue;
        _readJoinEntities = readJoinEntities;
        _findEntityType = findEntityType;
        try
        {
            return ToDynamicValue(value, type, shape);
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
    public virtual DynamicValueNode ToDynamicValue(object? value, Type type)
        => ToDynamicValue(value, type, declared: null);

    /// <summary>
    ///     Maps a value whose entity type the <em>caller</em> knows.
    /// </summary>
    /// <remarks>
    ///     An owned entity type is no more addressable by CLR type than a shared-type one: EF
    ///     names it for the navigation that owns it (`OwnedPerson.PersonAddress#OwnedAddress`),
    ///     and four of the owned types in `OwnedQueryTestBase`'s model are the same
    ///     <c>OwnedAddress</c>. The tracker fallback below cannot help either — not because the
    ///     server refuses to track, but because it tracks exactly what the client asked for and
    ///     these are no-tracking queries (A55). Two callers can name it anyway: whoever maps a
    ///     navigation's value knows the navigation and therefore its target (A52), and the query
    ///     itself names what it projects (<see cref="ProjectionShape" />, A56). Passed down through
    ///     collections so a collection navigation's *items* get it, and through a constructed row's
    ///     members.
    /// </remarks>
    internal DynamicValueNode ToDynamicValue(object? value, Type type, ProjectionShape? declared)
        => ToDynamicValue(value, type, declared, complexType: null);

    /// <summary>
    ///     Maps a <em>complex</em> value, whose <see cref="IComplexType" /> the caller knows.
    /// </summary>
    /// <remarks>
    ///     A complex value has no identity, so it travels as an object shape rather than as a key
    ///     — but the object shape is a <em>reflective</em> walk of the CLR type, and the CLR type
    ///     is not the model. `CompiledModelTestBase.OwnedType` declares
    ///     <c>public DbContext? Context</c> and its model says <c>Ignore(e =&gt; e.Context)</c>;
    ///     the walk sent it anyway and the far side answered *"Type
    ///     'Microsoft.EntityFrameworkCore.DbContext' is not on the deserialization allowlist"*
    ///     (C92). Handing the complex type down lets the walk drop what the model does not map.
    /// </remarks>
    internal DynamicValueNode ToComplexValue(object? value, IComplexProperty complexProperty)
        => ToDynamicValue(value, complexProperty.ClrType, declared: null, complexProperty.ComplexType);

    private DynamicValueNode ToDynamicValue(
        object? value,
        Type type,
        ProjectionShape? declared,
        IComplexType? complexType)
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

        return MapToNode(value, type, id, declared, complexType);
    }

    private DynamicValueNode MapToNode(
        object? value,
        Type type,
        int id,
        ProjectionShape? declared,
        IComplexType? complexType = null)
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

        // Last: what the caller said this is. An owned entity type has no CLR-type name and no
        // tracker entry to be found through, so the navigation that owns it is the only thing
        // that can name it.
        //
        // Only when the value really is one. A *collection* navigation hands its target entity
        // type down for the sake of the items, and the collection itself passes through here
        // first — an unguarded `??=` made a `HashSet<Order>` an `Order`, and the entity walk read
        // `Order`'s properties off it: "Unable to cast HashSet<Order> to Order".
        if (entityType is null && declared?.EntityType is { } named && named.ClrType.IsInstanceOfType(value))
        {
            entityType = named;
        }

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
                // A query-tree constant travels as identity only — except when there is no
                // identity. A keyless entity type has no key, so "identity only" carries
                // literally nothing: the server rebuilt an empty shell and
                // `Set<CustomerQuery>().Contains(x)` answered `false` against a row it had just
                // sent. Its values *are* what distinguishes it, so those are what travel. Scalars
                // only — the navigation walk below needs the row-mapping callbacks a constant
                // does not have.
                return new DynamicValueNode
                {
                    Id = id,
                    Type = typeNode,
                    EntityKey = entityKey,
                    Properties = entityType.FindPrimaryKey() is null ? MapScalars(value, entityType) : [],
                };
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

        // The application's value-mapper chain, which gets first refusal on anything that would
        // otherwise be walked reflectively. Placed here on purpose: *after* the primitive branch,
        // so a mapper cannot shadow a wire primitive, and *before* the collection and
        // object-shape branches, which are the two it exists to pre-empt. A custom type can be
        // enumerable, and the reflective walk is what overflows the stack on a geometry.
        foreach (ValueMapping.IInfoCarrierValueMapper mapper in _valueMappers)
        {
            if (!mapper.TryMapToWire(value, type, out object? wireValue))
            {
                continue;
            }

            // A claimed value has to produce something, because the reverse side keys on
            // `PrimitiveValue` being present. Declining is how a mapper says "not mine".
            if (wireValue is null)
            {
                throw new InvalidOperationException(
                    $"Value mapper '{mapper.GetType().Name}' claimed a value of type '{type}' but produced "
                        + "no wire value. A mapper that cannot map a value must return false instead.");
            }

            // `Nameable`, not `typeNode`: a value the chain claims may be a non-public
            // implementation type — `IPAddress.Loopback` is an `IPAddress+ReadOnlyIPAddress` —
            // and the receiving side has to be handed a name its allowlist admits and its
            // resolver can find. Safe *here* and nowhere else: an entity, a proxy, a primitive
            // and a `Type` value have all returned above, which is exactly what made the
            // general application of this rule cost 375 (C23).
            return new DynamicValueNode
            {
                Id = id,
                Type = _typeMapper.ToTypeNode(TypeNodeMapper.Nameable(type)),
                PrimitiveValue = PrimitiveCoercion.Normalize(wireValue),
            };
        }

        // Collection / array.
        if (value is IEnumerable enumerable && value is not string)
        {
            var items = new List<DynamicValueNode>();
            Type elementType = GetElementType(type);
            foreach (object? item in enumerable)
            {
                // The complex type descends with the items, because a complex *collection*
                // property is declared as the collection and mapped as its element.
                items.Add(ToDynamicValue(item, item?.GetType() ?? elementType, declared, complexType));
            }

            return new DynamicValueNode { Id = id, Type = typeNode, Items = items };
        }

        // Object shape: public readable properties *and* public fields (records/anonymous/DTOs
        // and plain structs). Fields are not an edge case — a struct written
        // `public int X, Y;` has no properties at all, and mapping only properties round-tripped
        // it as `default`.
        var properties = new List<DynamicPropertyValue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // The complex type filters this walk only when the walk is actually *of* it, and two
        // shapes that are not it reach here carrying one. Both cost a measured regression.
        //
        // A **property bag** complex type is a `Dictionary<string, object>`, which goes down the
        // collection branch, so the items one level further down are
        // `KeyValuePair<string, object>` — whose `Key` and `Value` no complex type declares.
        // Filtering those away rebuilt the bag with a null key: *"Value cannot be null.
        // (Parameter 'key')"*, twice, in `Can_track_entity_with_complex_property_bag_collections`.
        //
        // An **optional** complex property is declared `ValueNestedType?`, so what is walked here
        // is `Nullable<>`'s own `HasValue` and `Value`, which the complex type equally does not
        // declare. Dropping those sent nothing at all and five `Query.Associations.
        // ComplexProperties` tests answered `null` where a nested value belonged. `Nullable<>` is
        // a wrapper the model does not model: let its members through and hand the complex type
        // to `Value`, which is where the members it *does* declare actually are.
        bool nullableWrapper = complexType is not null && Nullable.GetUnderlyingType(type) == complexType.ClrType;
        IComplexType? shapeOf = complexType is not null
            && !nullableWrapper
            && complexType.ClrType.IsInstanceOfType(value)
                ? complexType
                : null;

        void AddMember(string name, Type memberType, object? memberValue)
        {
            if (!seen.Add(name))
            {
                return;
            }

            // Where the model knows this shape, the model decides what it holds. A complex type
            // is walked reflectively because it *is* its members — no identity, no navigations,
            // no sharing — but "its members" is the model's list, not the CLR type's, and an
            // `Ignore`d member is not one of them (C92). Only a complex type is filtered: an
            // anonymous type, a DTO or a constructed row has no model to ask, and there the walk
            // is the whole specification.
            if (shapeOf is not null
                && shapeOf.FindProperty(name) is null
                && shapeOf.FindComplexProperty(name) is null)
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
            Type memberDeclaredType = Nullable.GetUnderlyingType(memberType) ?? memberType;
            bool isPrimitive = IsPrimitive(memberValue);
            bool asPrimitive = isPrimitive
                && (memberValue is null || memberDeclaredType == memberValue.GetType());

            properties.Add(new DynamicPropertyValue
            {
                Name = name,

                // Normalize, as MapRowMembers does. An enum carries its own concrete runtime
                // type, which can never be pre-registered with the source-generated serializer
                // (ADR-008 constraint 8), so an un-normalized one fails at write time —
                // AutoTransactionBehavior on a captured DbContext did exactly that.
                PrimitiveValue = asPrimitive ? PrimitiveCoercion.Normalize(memberValue) : null,

                // A constructed row's member is where a directly projected owned entity actually
                // lands — `Select(p => new { p.PersonAddress, … })` — so the shape descends with it.
                Value = asPrimitive
                    ? null
                    : ToDynamicValue(
                        memberValue,
                        isPrimitive ? memberValue!.GetType() : memberType,
                        declared?.Member(name),

                        // Nested complex types are complex types too, and the filter has to reach
                        // them or it only cleans the top level.
                        nullableWrapper && name == nameof(Nullable<int>.Value)
                            ? complexType
                            : shapeOf?.FindComplexProperty(name)?.ComplexType),
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
    ///     Maps an entity's mapped <em>values</em> — every scalar through its
    ///     <see cref="IProperty" /> accessor, so shadow properties and value converters are
    ///     honoured where a public-reflection walk would miss them (ADR-008 constraint 1), plus
    ///     every complex property. No navigations: those are
    ///     <see cref="MapRowMembers" />'s half, and they need callbacks a query-tree constant
    ///     does not have.
    /// </summary>
    private List<DynamicPropertyValue> MapScalars(object value, IEntityType entityType)
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

        // A complex property is not in `GetProperties()` — EF keeps those in a list of their own —
        // so until this existed a complex member simply never travelled and arrived at its CLR
        // default. `Culture.Species` came back null out of a query that had just written it.
        //
        // Mapped as an ordinary object, nesting and all, which is right because that is what a
        // complex value *is*: owned, with no identity, no navigations and no sharing. The
        // reflective object shape is therefore the whole of it, and the receiving side rehydrates
        // it the same way. (The one thing it does not carry is a value converter or a shadow
        // property declared *inside* a complex type — those need the accessor walk above, one
        // level down.)
        foreach (IComplexProperty complexProperty in entityType.GetComplexProperties())
        {
            members.Add(new DynamicPropertyValue
            {
                Name = complexProperty.Name,
                Value = ToComplexValue(complexProperty.GetGetter().GetClrValue(value), complexProperty),
            });
        }

        return members;
    }

    /// <summary>
    ///     Maps an entity row's members: its values, plus the navigations EF actually loaded.
    /// </summary>
    private List<DynamicPropertyValue> MapRowMembers(object value, IEntityType entityType)
    {
        List<DynamicPropertyValue> members = MapScalars(value, entityType);

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
                        : ToDynamicValue(
                            navigation.GetGetter().GetClrValue(value),
                            navigation.ClrType,
                            ProjectionShape.For(navigation.TargetEntityType)),
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
    public virtual object? FromPropertyValue(DynamicPropertyValue property, Type declaredType)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property.Value is { } nested
            ? FromDynamicValue(nested)
            : PrimitiveCoercion.Coerce(property.PrimitiveValue, declaredType);
    }

    public virtual object? FromDynamicValue(DynamicValueNode node)
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
    public virtual void RegisterMaterialized(int id, object? value)
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
    public virtual object? FromShape(DynamicValueNode node)
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

        // The reverse of the value-mapper hook in MapToNode. Must precede the scalar branch: the
        // node carries a wire primitive under the value's *original* type, so `Coerce` would be
        // asked to turn a string into a `Point` and would fail. Keyed on the declared type, so a
        // chain that does not claim this type costs one predicate per registered mapper.
        if (node.PrimitiveValue is not null)
        {
            foreach (ValueMapping.IInfoCarrierValueMapper mapper in _valueMappers)
            {
                if (mapper.TryMapFromWire(node.PrimitiveValue, type, out object? mapped))
                {
                    return mapped;
                }
            }
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
    ///     <para>
    ///         That key is the whole point of the reference form: EF rewrites
    ///         <c>Contains(entity)</c> and <c>entity1 == entity2</c> into comparisons over key
    ///         values, so an instance without them silently matches nothing rather than failing.
    ///     </para>
    ///     <para>
    ///         A <b>keyless</b> entity type has none, so there is no reference form to rebuild —
    ///         <see cref="MapToNode" /> sends its values instead, and the ordinary object-shape
    ///         rehydration is what puts them back.
    ///     </para>
    /// </remarks>
    private object MaterializeEntityReference(DynamicValueNode node, IEntityType entityType)
    {
        IKey? primaryKey = entityType.FindPrimaryKey();
        if (primaryKey is null)
        {
            return RehydrateObject(node, entityType.ClrType)!;
        }

        object instance = Activator.CreateInstance(entityType.ClrType, nonPublic: true)!;
        RegisterMaterialized(node.Id, instance);

        if (node.EntityKey!.KeyValues.Count != primaryKey.Properties.Count)
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

        return CreateRange(type, elementType, items) ?? AddToNew(type, elementType, items);
    }

    /// <summary>
    ///     Builds <paramref name="type" /> by constructing it empty and adding each element —
    ///     EF's own first rule for a collection navigation, mirrored here.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>CollectionTypeFactory.TryFindTypeToInstantiate</c> instantiates the declared type
    ///         verbatim whenever it is concrete and declares a public parameterless constructor,
    ///         and only then falls back to a <c>HashSet</c> or a <c>List</c>. So a navigation
    ///         declared as <c>class MyGenericCollection&lt;T&gt; : List&lt;T&gt;</c> holds exactly
    ///         that on the server, and the wire says so — but nothing above could rebuild it: it
    ///         has no single-argument constructor, no set interface accepts it, and no static
    ///         factory returns it. The list fell through instead, and the cast to the declared type
    ///         failed one frame later, in <c>ClientResultMaterializer.Materialize</c> or inside a
    ///         constructor the rehydrator was invoking.
    ///     </para>
    ///     <para>
    ///         The declared constructor, not an inherited one: constructors are not inherited, and
    ///         the rule is EF's — a type whose only way in takes an argument
    ///         (<c>MyInvalidCollection(int)</c>) is one EF itself refuses to create, and refusing
    ///         it here keeps the two sides saying the same thing.
    ///     </para>
    /// </remarks>
    private static object? AddToNew(Type type, Type elementType, IList items)
    {
        if (type.IsInterface
            || type.IsAbstract
            || type.GetConstructor(Type.EmptyTypes) is not { IsPublic: true } constructor)
        {
            return null;
        }

        Type collection = typeof(ICollection<>).MakeGenericType(elementType);
        if (!collection.IsAssignableFrom(type))
        {
            return null;
        }

        object instance = constructor.Invoke([]);
        MethodInfo add = collection.GetMethod(nameof(ICollection<object>.Add))!;
        foreach (object? item in items)
        {
            add.Invoke(instance, [item]);
        }

        return instance;
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
    {
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        // What the sequence actually yields, which is not always its first generic argument. A
        // property-bag complex type is a `Dictionary<string, object>`, whose elements are
        // `KeyValuePair<string, object>` — taking the first argument built a `List<string>` and
        // then refused every element: "The value [Name, Clueless] is not of type System.String".
        Type? enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0]
            ?? (type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object));
    }
}
