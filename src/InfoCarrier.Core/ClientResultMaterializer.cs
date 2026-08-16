// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     Materializes query results on the client (requirements §2.5): deserializes rows,
///     resolves identity against the client change tracker (reuse tracked instance / attach
///     new), populates scalar properties, and wires navigation fixup. Non-entity projections
///     are materialized from columnar data (E2).
/// </summary>
/// <remarks>
///     Identity resolution and tracking go through EF's internal <see cref="IStateManager" />
///     (the v1 pattern): <c>TryGetEntry</c> for the identity map, <c>GetOrCreateEntry</c> +
///     <c>SetEntityState(Unchanged)</c> to attach — which bypasses the public-API value
///     generation that would otherwise fire for store-generated keys (the server owns key
///     generation; the client never suppresses it in the shared model).
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="ClientResultMaterializer" /> class.
/// </remarks>
public class ClientResultMaterializer(
    DbContext context,
    IExpressionSerializer serializer,
    QueryTrackingBehavior trackingBehavior,
    bool deferTracking = false)
{
    private readonly DbContext _context = context;
    private readonly IStateManager _stateManager = context.GetService<IStateManager>();
    private readonly ExpressionSerializer _serializer = (ExpressionSerializer)serializer;
    private readonly QueryTrackingBehavior _trackingBehavior = trackingBehavior;
    private readonly bool _deferTracking = deferTracking;

    // Identity map for entities materialized but not yet attached. The state manager's own map
    // only holds tracked entries, so while tracking is deferred it cannot answer "have I already
    // built this one?" — and two rows naming the same customer would become two instances.
    private readonly Dictionary<(IEntityType Type, string Key), (object Instance, InternalEntityEntry Entry)> _deferredIdentity = [];

    /// <summary>
    ///     Entities materialized but not yet attached, when tracking is deferred
    ///     (<c>docs/projection-split.md</c> §4).
    /// </summary>
    /// <remarks>
    ///     A split query materializes every row the server sent, but only the entities the
    ///     <em>residual</em> yields belong in the change tracker. Attaching at materialization
    ///     time tracked all 919 rows of a join whose projection returned 7 entities, and a query
    ///     projecting no entities at all still filled the tracker.
    /// </remarks>
    public virtual IReadOnlyDictionary<object, InternalEntityEntry> Deferred => _deferred;

    private readonly Dictionary<object, InternalEntityEntry> _deferred = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     Attaches one deferred entity, if it is one this materializer built.
    /// </summary>
    public virtual void AttachIfDeferred(object entity)
    {
        if (_deferred.Remove(entity, out InternalEntityEntry? entry)
            && entry.EntityState == EntityState.Detached)
        {
            entry.SetEntityState(EntityState.Unchanged);
        }
    }

    /// <summary>
    ///     Materializes the wire result into a sequence of <typeparamref name="TElement" />.
    /// </summary>
    public virtual IEnumerable<TElement> Materialize<TElement>(QueryDataResult result)
    {
        var mapper = (DynamicValueMapper)_serializer.ValueMapper;

        // Entities are built here wherever they appear in the graph — as a whole row, or nested
        // inside a projection (`Select(c => new { c, o })`). Before this hook the nested case
        // was reflection-constructed by the mapper: detached, unregistered, no shadow state.
        //
        // Saved and restored, because a query can now nest inside another one: a lazy load
        // issued while an outer result is still being decoded runs a whole exchange of its own,
        // and the mapper is DI-scoped and shared. Without this the inner materializer — with its
        // own tracking behaviour, deferred-identity map and reference scope — replaced the
        // outer's for the rest of the outer decode and was never put back.
        Func<DynamicValueNode, object?>? outer = mapper.EntityMaterializer;
        mapper.EntityMaterializer = node => MaterializeEntity(node, mapper);

        // Decoded to completion before anything is handed out, for the same reason: yielding
        // lazily lets the caller start a nested exchange part-way through this one, and this
        // method's own hook would then already have been replaced. The payload is deserialized
        // into a list of nodes up front regardless, so nothing extra is held.
        var rows = new List<TElement>();

        try
        {
            foreach (DynamicValueNode row in DeserializeRows(result))
            {
                // A null row is data, not an absent row. `Select(c => c.Orders.FirstOrDefault())`
                // over a customer with no orders produces one, and skipping it silently returned
                // 89 rows where the query defined 91.
                object? value = mapper.FromDynamicValue(row);

                rows.Add(value is null ? default! : (TElement)value);
            }

            AttachJoinRows(rows);
        }
        finally
        {
            mapper.EntityMaterializer = outer;
        }

        return rows;
    }

    /// <summary>
    ///     Join rows built but not yet tracked, with the entity each was sent for, in the order
    ///     the serializer's walk produced them.
    /// </summary>
    private readonly List<(object Owner, object JoinEntity)> _pendingJoinRows = [];

    /// <summary>
    ///     Set while a join row is being built, so that it goes to <see cref="_deferred" />
    ///     whether or not this query defers tracking generally.
    /// </summary>
    private bool _holdJoinRow;

    /// <summary>
    ///     Tracks the held-back join rows in <em>result</em> order.
    /// </summary>
    /// <remarks>
    ///     A skip navigation is rebuilt by EF's fixup as the join rows behind it are tracked, so
    ///     the order they are tracked in is the order a <c>List</c>-backed navigation comes out
    ///     in — which is why <c>Assert.Equal(children, left.TwoSkipShared.ToList())</c> can see
    ///     it. Attaching them where they arrive gave the serializer's depth-first walk instead:
    ///     <c>Load_collection_using_Query_with_Include</c> returns EntityTwo 10, 11, 16, and
    ///     EntityTwo 16 is reachable from row 10's own include
    ///     (<c>10 → ThreeSkipFull → EntityThree 3 → TwoSkipFull → 16</c>), so it is serialized —
    ///     join rows and all — inside row 10 and the navigation came out 10, 16, 11.
    ///     <para>
    ///         So a result element's join rows attach at the element's position. Rows sent for an
    ///         entity that is <em>not</em> a result element — the loaded far side of A7, which
    ///         sends the whole set in one run — keep the order they arrived in, which is the run
    ///         order that matters there.
    ///     </para>
    /// </remarks>
    private void AttachJoinRows<TElement>(List<TElement> rows)
    {
        if (_pendingJoinRows.Count == 0)
        {
            return;
        }

        List<(object Owner, object JoinEntity)> pending = [.. _pendingJoinRows];
        _pendingJoinRows.Clear();

        var byOwner = new Dictionary<object, List<object>>(ReferenceEqualityComparer.Instance);
        foreach ((object owner, object joinEntity) in pending)
        {
            if (!byOwner.TryGetValue(owner, out List<object>? joinEntities))
            {
                byOwner[owner] = joinEntities = [];
            }

            joinEntities.Add(joinEntity);
        }

        var flushed = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (TElement element in rows)
        {
            if (element is not null
                && byOwner.TryGetValue(element, out List<object>? joinEntities)
                && flushed.Add(element))
            {
                joinEntities.ForEach(AttachIfDeferred);
            }
        }

        foreach ((object owner, object joinEntity) in pending)
        {
            if (!flushed.Contains(owner))
            {
                AttachIfDeferred(joinEntity);
            }
        }
    }

    private static List<DynamicValueNode> DeserializeRows(QueryDataResult result)
        => System.Text.Json.JsonSerializer.Deserialize(
            result.SerializedResults, ExpressionJsonContext.Default.ListDynamicValueNode) ?? [];

    /// <summary>
    ///     Materializes one entity row: identity resolution first, then scalars, then
    ///     navigations (requirements §2.5).
    /// </summary>
    /// <remarks>
    ///     Entity rows are built here rather than in <see cref="DynamicValueMapper" /> because
    ///     shadow-state values can only be written through the change tracker. The instance is
    ///     registered under its wire id <em>before</em> members are populated, so a navigation
    ///     that points back at it resolves to this instance instead of recursing
    ///     (<c>docs/result-wire-format.md</c> §3.1).
    /// </remarks>
    private object? MaterializeEntity(DynamicValueNode row, DynamicValueMapper mapper)
    {
        // Consumed here, before anything nested is built: the hold belongs to *this* row, and a
        // join row carries navigations to both of its sides. Leaving it set deferred those too,
        // and nothing ever attached them — 150 of the 482 `ManyToMany*Load` tests.
        bool holdThisRow = _holdJoinRow;
        _holdJoinRow = false;

        IEntityType? entityType = _context.Model.FindEntityType(row.EntityKey!.EntityTypeName);
        if (entityType is null)
        {
            // Not FromDynamicValue: that would route straight back into this method.
            return mapper.FromShape(row);
        }

        IKey? pk = entityType.FindPrimaryKey();

        // A no-tracking query must leave the change tracker untouched, and a keyless entity type
        // can never be tracked at all. Attaching regardless was invisible while included
        // navigations were being dropped; once they arrive, attaching a graph the tracker cannot
        // key throws "Unable to track an entity … its primary key property is null".
        //
        // `IsTracked` covers the case neither of those catches: an entity *constructed by a
        // projection* — `Select(o => new Order { OrderDate = … })` — which EF does not track
        // even under TrackAll. Those rows all carry a default key, so identity-resolving them
        // collapsed an entire result onto its first instance.
        if (pk is null || !row.IsTracked || _trackingBehavior != QueryTrackingBehavior.TrackAll)
        {
            return MaterializeUntracked(row, entityType, mapper);
        }

        // Identity resolution via EF's own identity map (v1 pattern): reuse the tracked
        // instance if an entity with the same key is already tracked.
        if (row.EntityKey.KeyValues.Count == pk.Properties.Count)
        {
            object?[] keyValues = pk.Properties
                .Select((p, i) => PrimitiveCoercion.FromWireValue(p, row.EntityKey.KeyValues[i]))
                .ToArray();

            InternalEntityEntry? tracked = _stateManager.TryGetEntry(pk, keyValues);
            if (tracked is not null)
            {
                mapper.RegisterMaterialized(row.Id, tracked.Entity);
                PopulateNavigations(row, tracked.Entity, entityType, tracked, mapper);
                return tracked.Entity;
            }
        }

        // While tracking is deferred the state manager cannot answer identity, so a local map
        // does. Same rule, same keys — only the place the answer is kept differs.
        string? deferredKey = _deferTracking && row.EntityKey.KeyValues.Count == pk.Properties.Count
            ? string.Join('', row.EntityKey.KeyValues)
            : null;

        if (deferredKey is not null && _deferredIdentity.TryGetValue((entityType, deferredKey), out var already))
        {
            mapper.RegisterMaterialized(row.Id, already.Instance);

            // Still walk the row. Its nested nodes carry wire ids that later rows may reference,
            // and returning early stranded them — "dangling wire reference 2: no value with that
            // id has been materialized".
            PopulateNavigations(row, already.Instance, entityType, already.Entry, mapper);
            return already.Instance;
        }

        // Scalars first, because the entity is built *from* them. Ordering this the other way —
        // construct, then assign — is what `Activator.CreateInstance` forced, and it is why no
        // entity ever had a working `ILazyLoader`: reflection-constructing an instance skips
        // EF's materializer, so constructor binding and service-property injection never run and
        // every service property stayed null. `CreateEntry` runs
        // `entityType.GetOrCreateMaterializer(...)`, which does both. (v1 built entities this way
        // for the same reason; see its `InfoCarrierQueryResultMapper.TryMapEntity`.)
        //
        // Nothing here can back-reference the row being built: these are scalar properties, and
        // a scalar cannot point at its own entity. That is what lets the values be resolved
        // before the instance exists and before its wire id is registered below.
        // Detached, as `GetOrCreateEntry` left it: the state below is set deliberately, and
        // going through the internal state manager still bypasses the public-API value
        // generation that would otherwise fire for store-generated keys.
        Dictionary<string, object?> values = ReadPrimitives(row, entityType);

        // The scalars whose value travels as a node rather than as a primitive join the same
        // dictionary, so the entity is built from *all* of its values at once. They used to be
        // written onto the entry afterwards, which the from-query path below would read as
        // modifications to a row that had only just been read.
        foreach ((IProperty property, DynamicValueNode node) in ComplexScalars(row, entityType))
        {
            values[property.Name] = PrimitiveCoercion.FromWireValue(property, mapper.FromDynamicValue(node));
        }

        InternalEntityEntry entry;
        object instance;

        if (_deferTracking || holdThisRow)
        {
            // Left Detached, so it is invisible to ChangeTracker.Entries() until the residual
            // shows it actually reached the result — or, for a join row, until the result order
            // is known (`AttachJoinRows`).
            entry = _stateManager.CreateEntry(values, entityType);
            instance = entry.Entity;

            if (deferredKey is not null)
            {
                _deferredIdentity[(entityType, deferredKey)] = (instance, entry);
            }

            mapper.RegisterMaterialized(row.Id, instance);
            ClearPlaceholderReferencesBlockingFixup(instance, entityType, values);
            ApplyComplexValues(row, instance, entityType, mapper);
            _deferred[instance] = entry;
        }
        else
        {
            instance = Materialize(entityType, values);
            mapper.RegisterMaterialized(row.Id, instance);
            ClearPlaceholderReferencesBlockingFixup(instance, entityType, values);
            ApplyComplexValues(row, instance, entityType, mapper);

            // Tracked *as coming from a query*, which is what these rows are, and which EF's own
            // shaper does through exactly this call. `SetEntityState(Unchanged)` is the attach
            // path and differs in two ways that both cost tests:
            //
            //  - it snapshots by reading the entity's properties, so a mapped property whose
            //    getter touches a navigation issues a lazy load *while the entry is still
            //    Detached* — `EntityFinder.Query` then picks `AsNoTracking()` and the principal
            //    is never tracked (L24, `Lazy_load_one_to_one_reference_with_recursive_property`);
            //  - `NavigationFixer.InitialFixup` guards its dependent-side writes with
            //    `!fromQuery || CanOverrideCurrentValue(…)`, so only the from-query path leaves a
            //    navigation the caller has already pointed at another *tracked* entity alone —
            //    which is what `Fixup_reference_after_FK_change_without_DetectChanges` asserts.
            //
            // The snapshot argument carries shadow state, exactly as
            // `InternalEntityEntry`'s values constructor builds it.
            entry = _stateManager.StartTrackingFromQuery(
                entityType,
                instance,
                ((IRuntimeEntityType)entityType).ShadowValuesFactory(values));
        }

        PopulateNavigations(row, instance, entityType, entry, mapper);

        return instance;
    }

    /// <summary>
    ///     Nulls a constructor-initialized reference navigation, but only where it would block
    ///     a fixup that ought to happen — that is, where the principal is already tracked.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF's <c>NavigationFixer</c> overwrites a navigation only when it is null or
    ///         already points at the principal; tracking <em>from a query</em> additionally
    ///         overrides a value that is not itself tracked. We attach through
    ///         <c>SetEntityState</c>, which is not the from-query path, so a constructor-set
    ///         placeholder blocks fixup outright and an attached order never joined its
    ///         customer's <c>Orders</c>.
    ///     </para>
    ///     <para>
    ///         Northwind's <c>Order.Customer</c> is initialized to <c>new()</c> deliberately, as
    ///         the regression test for EF issue #23851 — and that placeholder must
    ///         <em>survive</em> when the principal is absent, which
    ///         <c>Include_collection_dependent_already_tracked</c> asserts directly. So the
    ///         condition is not "was it constructor-initialized" but "is there a real principal
    ///         for it to be replaced by", which is exactly when EF's own fixup would act.
    ///     </para>
    /// </remarks>
    private void ClearPlaceholderReferencesBlockingFixup(
        object instance,
        IEntityType entityType,
        Dictionary<string, object?> values)
    {
        foreach (IForeignKey foreignKey in entityType.GetForeignKeys())
        {
            if (foreignKey.DependentToPrincipal is not { PropertyInfo: { CanWrite: true } clrProperty } navigation)
            {
                continue;
            }

            // Read through the backing field, never the property. The property getter on a
            // lazy-loading entity *is* the lazy load: reading it here fired a query during
            // materialization and left the navigation marked loaded, so a test that had only
            // queried the dependent was told its principal reference was already loaded. Since
            // L1 builds entities with EF's materializer, every lazy-loading shape reaches this
            // code, and only the field read is free of side effects.
            object? current = navigation.FieldInfo is { } field
                ? field.GetValue(instance)
                : clrProperty.GetValue(instance);

            if (current is null)
            {
                continue;
            }

            object?[] keyValues = foreignKey.Properties
                .Select(p => values.TryGetValue(p.Name, out object? v) ? v : null)
                .ToArray();
            if (Array.Exists(keyValues, v => v is null))
            {
                continue;
            }

            if (_stateManager.TryGetEntry(foreignKey.PrincipalKey, keyValues) is not null)
            {
                clrProperty.SetValue(instance, null);
            }
        }
    }

    /// <summary>
    ///     Materializes an entity that must not enter the change tracker: a no-tracking result,
    ///     or a keyless entity type.
    /// </summary>
    /// <remarks>
    ///     Shadow properties are skipped, because there is nowhere to put them — an untracked
    ///     instance has no state entry, and EF's own no-tracking results carry no shadow state
    ///     either. Identity resolution is likewise absent by design: under
    ///     <see cref="QueryTrackingBehavior.NoTracking" /> EF returns a fresh instance per row,
    ///     and under <see cref="QueryTrackingBehavior.NoTrackingWithIdentityResolution" /> the
    ///     server already collapsed duplicates, which the wire preserves as back-references.
    /// </remarks>
    private object MaterializeUntracked(DynamicValueNode row, IEntityType entityType, DynamicValueMapper mapper)
    {
        // EF's materializer, but *without* a state-manager entry. The materializer is what gives
        // the instance its `ILazyLoader` — a no-tracking query still returns entities that may
        // lazy-load — so reflection-constructing it here has the same defect as it did on the
        // tracked path.
        //
        // Going through `CreateEntry` instead would be simpler and is wrong: it registers the
        // entry in the state manager's reference map, and a second untracked instance of the same
        // row then collided with the first the moment anything attached one — "another instance
        // with the key value '{AlternateId: Root}' is already being tracked", 109 times. EF's own
        // no-tracking path creates no entry either; this is that path, reproduced.
        object instance = Materialize(entityType, ReadPrimitives(row, entityType));

        mapper.RegisterMaterialized(row.Id, instance);

        foreach ((IProperty property, DynamicValueNode node) in ComplexScalars(row, entityType))
        {
            if (property.PropertyInfo is { CanWrite: true } clrProperty)
            {
                clrProperty.SetValue(
                    instance, PrimitiveCoercion.FromWireValue(property, mapper.FromDynamicValue(node)));
            }
        }

        // A complex value is not a scalar and not a navigation, so neither of the two loops above
        // it is the one that carries it — the tracked path calls this and this path did not, which
        // simply dropped every complex member of every no-tracking result. `ComplexTypeQuery`'s
        // fixture is no-tracking throughout, and 62 of its assertions were reading a `null`
        // `Customer.ShippingAddress` the server had sent in full (B11).
        ApplyComplexValues(row, instance, entityType, mapper);

        PopulateNavigations(row, instance, entityType, entry: null, mapper);
        return instance;
    }



    /// <summary>
    ///     Records a navigation as loaded on an entity that has no entry, through its own lazy
    ///     loader.
    /// </summary>
    /// <remarks>
    ///     An untracked entity has nowhere to keep loaded-state except the <c>ILazyLoader</c>
    ///     that was injected into it, and without this a navigation the server already sent is
    ///     indistinguishable from one that was never loaded — so the loader fetches it again, or
    ///     the test simply asks and is told `false`. v1 did the same thing, in its
    ///     <c>SetIsLoadedNoTracking</c>.
    ///     <para>
    ///         The loader is looked for on <paramref name="entityType" /> — the type the row
    ///         actually is — and not on the navigation's declaring type. `UseLazyLoadingProxies`
    ///         adds the service property only to types it can proxy, so an **abstract** base
    ///         declaring a navigation never gets one: `LazyLoadProxyTestBase.Parent` is abstract
    ///         and declares `Children`, while the row is a `Mother`. Asking `Parent` found nothing
    ///         and the collection came back reporting itself unloaded.
    ///     </para>
    /// </remarks>
    private static void SetIsLoadedUntracked(object instance, IEntityType entityType, INavigationBase navigation)
    {
        IServiceProperty? loaderProperty = entityType
            .GetServiceProperties()
            .FirstOrDefault(p => p.ClrType == typeof(ILazyLoader));

        if (loaderProperty?.GetGetter().GetClrValue(instance) is ILazyLoader loader)
        {
            loader.SetLoaded(instance, navigation.Name);
        }
    }

    /// <summary>
    ///     Swaps a freshly materialized navigation target for the tracked instance with the same
    ///     key, when the entity holding the navigation is itself tracked.
    /// </summary>
    /// <remarks>
    ///     Assigning a navigation on a tracked entity wakes EF's <c>NavigationFixer</c>, which
    ///     attaches the whole graph reachable from what was assigned. Hand it a second instance
    ///     of an already-tracked row and it throws an identity conflict — "another instance with
    ///     the key value '{AlternateId: Root}' is already being tracked".
    ///     <para>
    ///         This was unreachable until lazy loading started working: a lazy load is the case
    ///         where a row arrives carrying a principal the client already holds. The owner being
    ///         tracked is the whole condition — an untracked owner has no fixer watching it, and
    ///         no-tracking results are supposed to be fresh instances.
    ///     </para>
    /// </remarks>
    private object? ResolveAgainstTracker(object? related, INavigationBase navigation, InternalEntityEntry? entry)
    {
        if (related is null || entry is null || entry.EntityState == EntityState.Detached)
        {
            return related;
        }

        IEntityType target = navigation.TargetEntityType;
        if (target.FindPrimaryKey() is not { } pk
            || pk.Properties.Any(p => p.IsShadowProperty()))
        {
            // A shadow key cannot be read off the instance, and there is nothing to match on.
            return related;
        }

        object?[] keyValues = [.. pk.Properties.Select(p => p.GetGetter().GetClrValue(related))];
        if (Array.Exists(keyValues, v => v is null))
        {
            return related;
        }

        object? tracked = _stateManager.TryGetEntry(pk, keyValues)?.Entity;

        // Only if it actually fits the navigation. A defensive guard, not a measured fix: it
        // never fires in the current suite. It is here because a PK-to-PK one-to-one gives two
        // entity types the same key value — `Parent` and `SinglePkToPk` are both id 707 — and
        // a lookup by key alone cannot tell them apart.
        return tracked is not null
            && (navigation.PropertyInfo?.PropertyType ?? navigation.ClrType).IsInstanceOfType(tracked)
                ? tracked
                : related;
    }

    /// <summary>
    ///     Builds an entity instance through EF's own materializer, with no state-manager entry.
    /// </summary>
    /// <remarks>
    ///     The body is <c>InternalEntityEntry</c>'s values constructor with the entry left out:
    ///     lay the values into a buffer indexed by <c>IProperty.GetIndex()</c>, then invoke the
    ///     entity type's materializer. That is what performs constructor binding — including an
    ///     <c>ILazyLoader</c> constructor parameter — and service-property injection.
    /// </remarks>
    private object Materialize(IEntityType entityType, Dictionary<string, object?> values)
    {
        var runtimeType = (IRuntimeEntityType)entityType;
        var buffer = new object?[runtimeType.PropertyCount];

        foreach (IProperty property in entityType.GetFlattenedProperties())
        {
            int index = property.GetIndex();
            if (index < 0)
            {
                continue;
            }

            buffer[index] = values.TryGetValue(property.Name, out object? value) ? value : property.Sentinel;
        }

        return MaterializerFor(runtimeType)(new MaterializationContext(new ValueBuffer(buffer), _context));
    }

    /// <summary>
    ///     The materializer to build an untracked instance with, which depends on the tracking
    ///     behavior the query asked for.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>GetOrCreateMaterializer</c> is the cached one, and it bakes
    ///         <c>QueryTrackingBehavior = null</c> into the compiled expression —
    ///         <c>StructuralTypeMaterializerSource.GetMaterializer</c> passes null for "not from a
    ///         query". That constant reaches <c>ILazyLoader.Injected</c>, and the loader reads it
    ///         back to decide `LoadOptions`: only
    ///         <see cref="QueryTrackingBehavior.NoTrackingWithIdentityResolution" /> asks
    ///         <c>EntityFinder</c> for <c>ForceIdentityResolution</c>, which is what stops a lazy
    ///         load appending a row the navigation already holds.
    ///     </para>
    ///     <para>
    ///         So under that behavior a lazily loaded collection came back with duplicates —
    ///         `Lazy_load_collection_already_partially_loaded` counted 3 where 2 were expected,
    ///         and `..._already_loaded_delegate_loader_*` counted 4 where 2 were. EF's query
    ///         pipeline compiles its own materializer with the real behavior for exactly this
    ///         reason; this does the same.
    ///     </para>
    ///     <para>
    ///         <b>For every behaviour, not just that one.</b> It began as a fix aimed at
    ///         <see cref="QueryTrackingBehavior.NoTrackingWithIdentityResolution" /> and left the
    ///         cached materializer everywhere else, on the argument that nothing else should pay for
    ///         it. That was wrong twice over: the compiled materializer is cached here too, so
    ///         nothing pays anything, and the baked-in <see langword="null" /> is *also* what
    ///         <c>MaterializationInterceptionData.QueryTrackingBehavior</c> reports —
    ///         `Assert.Equal(TrackAll, …)` against `null`, twelve times (B15). Every row this class
    ///         builds came from a query, so the behaviour is never genuinely unknown, and saying so
    ///         is the whole of the fix.
    ///     </para>
    /// </remarks>
    private Func<MaterializationContext, object> MaterializerFor(IRuntimeEntityType entityType)
        => QueryMaterializers.GetOrAdd(
            (entityType, _trackingBehavior),
            static (key, source) =>
            {
                var contextParameter = System.Linq.Expressions.Expression.Parameter(
                    typeof(MaterializationContext), "materializationContext");

                return System.Linq.Expressions.Expression.Lambda<Func<MaterializationContext, object>>(
                        source.CreateMaterializeExpression(
                            new Microsoft.EntityFrameworkCore.Query.StructuralTypeMaterializerSourceParameters(
                                key.EntityType,
                                "instance",
                                key.EntityType.ClrType,
                                key.TrackingBehavior),
                            contextParameter),
                        contextParameter)
                    .Compile();
            },
            _stateManager.EntityMaterializerSource);

    // Keyed by entity type *and* tracking behaviour: an entity type belongs to a model and lives
    // as long as the service provider that built it, and there are four behaviours, so this is
    // bounded by the models in play rather than by the queries run.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        (IRuntimeEntityType EntityType, QueryTrackingBehavior TrackingBehavior),
        Func<MaterializationContext, object>> QueryMaterializers = new();

    /// <summary>
    ///     Reads a row's <em>primitive</em> scalar values, keyed by property name for
    ///     <c>IStateManager.CreateEntry</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Keyed by name because that is how <c>CreateEntry</c> indexes them, which is also
    ///         what carries shadow properties — EF hands the dictionary to
    ///         <c>ShadowValuesFactory</c>. A property the row omits takes its sentinel rather
    ///         than a CLR default, which is EF's own rule for an absent value.
    ///     </para>
    ///     <para>
    ///         <b>Primitives only.</b> Everything the entity needs in order to be constructed —
    ///         keys above all — is primitive, while a value carried as a node has to be resolved
    ///         through the mapper. Resolving through the mapper before this row's own wire id is
    ///         registered would reorder the back-reference ids of the whole graph, so the
    ///         node-valued properties are applied by <see cref="ComplexScalars" /> after
    ///         registration — which is exactly where they were applied before this method
    ///         existed. Measured as neutral against reading every value up front; it is kept
    ///         because it preserves the established ordering rather than because it moved a
    ///         number.
    ///     </para>
    /// </remarks>
    private static Dictionary<string, object?> ReadPrimitives(DynamicValueNode row, IEntityType entityType)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (DynamicPropertyValue member in row.Properties)
        {
            if (member.IsLoadedNavigation || member.Value is not null)
            {
                continue;
            }

            if (entityType.FindProperty(member.Name) is { } property)
            {
                values[property.Name] = PrimitiveCoercion.FromWireValue(property, member.PrimitiveValue);
            }
        }

        return values;
    }

    /// <summary>
    ///     Writes a row's complex-property values onto the materialized instance.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A complex property cannot ride in the value dictionary the entity is built from.
    ///         <c>CreateEntry</c> and <c>ShadowValuesFactory</c> are keyed by property
    ///         <em>name</em>, and <c>GetFlattenedProperties()</c> gives each complex leaf its own
    ///         name — so <c>Blog.Culture.Species</c> and <c>Blog.Milk.Species</c> are both
    ///         <c>"Species"</c> and one would silently overwrite the other. The whole complex
    ///         value is therefore set through its CLR member instead.
    ///     </para>
    ///     <para>
    ///         Before the row is tracked, so the entry's first snapshot already holds it —
    ///         writing afterwards would make the from-query path read a row it had only just
    ///         materialized as modified.
    ///     </para>
    ///     <para>
    ///         Through the backing field where there is one, for the same reason a navigation is
    ///         (plan L6): on a proxy the property setter is intercepted, and this is
    ///         materialization, not a change.
    ///     </para>
    /// </remarks>
    private static void ApplyComplexValues(
        DynamicValueNode row, object instance, IEntityType entityType, DynamicValueMapper mapper)
    {
        foreach (DynamicPropertyValue member in row.Properties)
        {
            if (member.IsLoadedNavigation
                || member.Value is null
                || entityType.FindComplexProperty(member.Name) is not { } complexProperty)
            {
                continue;
            }

            object? value = mapper.FromDynamicValue(member.Value);

            if (complexProperty.FieldInfo is { } field)
            {
                field.SetValue(instance, value);
            }
            else if (complexProperty.PropertyInfo is { CanWrite: true } property)
            {
                property.SetValue(instance, value);
            }
        }
    }

    /// <summary>
    ///     The row's scalar properties whose value travels as a node rather than a primitive.
    /// </summary>
    private static IEnumerable<(IProperty Property, DynamicValueNode Node)> ComplexScalars(
        DynamicValueNode row,
        IEntityType entityType)
    {
        foreach (DynamicPropertyValue member in row.Properties)
        {
            if (member.IsLoadedNavigation || member.Value is null)
            {
                continue;
            }

            if (entityType.FindProperty(member.Name) is { } property)
            {
                yield return (property, member.Value);
            }
        }
    }

    /// <summary>
    ///     Wires loaded navigations. <paramref name="entry" /> is <see langword="null" /> for an
    ///     untracked instance, which simply has no place to record loaded state.
    /// </summary>
    private void PopulateNavigations(
        DynamicValueNode row,
        object instance,
        IEntityType entityType,
        InternalEntityEntry? entry,
        DynamicValueMapper mapper)
    {
        foreach (DynamicPropertyValue member in row.Properties)
        {
            // Join rows behind a skip navigation. Materializing them is the whole job — each is
            // an ordinary entity row, so `MaterializeEntity` tracks it with its payload and its
            // join-table foreign keys intact. Nothing is assigned to a navigation here; EF's own
            // fixup wires the graph once the rows are tracked.
            if (member.IsJoinRows)
            {
                // Recorded against the entity they were sent for, and materialized once the whole
                // result is decoded (<see cref="AttachJoinRows" />). Tracking a join row is what
                // makes EF append to the navigation it rebuilds, so the order they are tracked in
                // *is* the navigation's order — and the order they arrive in is the serializer's
                // depth-first walk, not the result's.
                foreach (DynamicValueNode joinRow in member.Value?.Items ?? [])
                {
                    // Built here — a wire reference id is only resolvable while its own message
                    // is being decoded, and a lazy load fired mid-decode resets that scope — but
                    // held back from the change tracker until the result order is known.
                    _holdJoinRow = true;
                    try
                    {
                        if (mapper.FromDynamicValue(joinRow) is { } joinEntity)
                        {
                            _pendingJoinRows.Add((instance, joinEntity));
                        }
                    }
                    finally
                    {
                        // Cleared unconditionally: a join row already materialized under another
                        // owner comes back as a wire back-reference and never reaches
                        // `MaterializeEntity` to consume the flag.
                        _holdJoinRow = false;
                    }
                }

                continue;
            }

            if (!member.IsLoadedNavigation)
            {
                continue;
            }

            INavigationBase? navigation = entityType.FindNavigation(member.Name)
                ?? (INavigationBase?)entityType.FindSkipNavigation(member.Name);
            if (navigation is null)
            {
                continue;
            }

            // Loaded, but with no value to carry: a *shadow* navigation has no CLR member, so
            // there is nothing to assign and nothing the client could read it back from. The
            // loaded flag is still real state — `Load_collection_already_loaded_untyped_*`
            // asserts `IsLoaded` on exactly such a navigation, named as a string because there is
            // no property to name it with — and it is all this member exists to carry. The join
            // rows sent alongside are what actually populate it.
            if (member.Value is null)
            {
                if (entry is not null)
                {
                    entry.SetIsLoaded(navigation);
                }
                else
                {
                    SetIsLoadedUntracked(instance, entityType, navigation);
                }

                continue;
            }

            // A collection is *filled*, never assigned — `MaterializeCollection` goes through the
            // model's own accessor and has already written whatever needed writing.
            object? related = navigation.IsCollection
                ? MaterializeCollection(member.Value, navigation, instance, mapper)
                : ResolveAgainstTracker(mapper.FromDynamicValue(member.Value), navigation, entry);

            if (related is not null && !navigation.IsCollection)
            {
                // The backing field first, and the property only when there is no field. L6
                // established this for *reads* — a property getter on a lazy-loading entity is
                // itself a load — and the write side is the same rule for a different reason: a
                // setter can refuse. `FieldMappingTestBase.PostFull.Blog` throws
                // `InvalidOperationException` outright unless the model is seeding, which is how
                // that base states "materialize through the field". EF's own materializer obeys
                // the navigation's `PropertyAccessMode`, whose default prefers the field.
                if (navigation.FieldInfo is { } field)
                {
                    field.SetValue(instance, related);
                }
                else if (navigation.PropertyInfo is { CanWrite: true } property)
                {
                    property.SetValue(instance, related);
                }
            }

            // The relationship snapshot is EF's record of what a navigation held when it was
            // last known-good, and it is what `DetectChanges` compares against. Assigning the
            // navigation through its CLR property — which is how a materialized graph is wired
            // here — leaves the snapshot empty, so EF sees every loaded relationship as newly
            // added. `VerifyRelationshipSnapshots` in the many-to-many spec asserts exactly this.
            if (entry is not null && related is not null && navigation.GetRelationshipIndex() >= 0)
            {
                if (navigation.IsCollection)
                {
                    foreach (object? item in (System.Collections.IEnumerable)related)
                    {
                        if (item is not null)
                        {
                            entry.AddToCollectionSnapshot(navigation, item);
                        }
                    }
                }
                else
                {
                    entry.SetRelationshipSnapshotValue(navigation, related);
                }
            }

            // Distinguishes a loaded-but-empty navigation from an unloaded one
            // (requirements §2.5 step 5).
            if (entry is not null)
            {
                entry.SetIsLoaded(navigation);
            }
            else
            {
                SetIsLoadedUntracked(instance, entityType, navigation);
            }
        }
    }

    /// <summary>
    ///     Fills a collection navigation, through the model's own collection accessor.
    /// </summary>
    /// <remarks>
    ///     Building a <c>List&lt;T&gt;</c> and assigning it works only where the navigation is
    ///     willing to take one, and `FieldMappingTestBase` is a catalogue of the ones that are
    ///     not: `BlogReadOnly._posts` is an `ObservableCollection<T>` and
    ///     `BlogReadOnlyExplicit._myposts` a `Collection<T>` — neither of which a list can be
    ///     assigned to — while `BlogFull.Posts` has a setter that throws outright unless the model
    ///     is seeding. The accessor answers all three, because it is what EF's own materializer
    ///     uses: it knows the concrete type to instantiate, the member to reach it through, and
    ///     that an existing collection is to be *filled* rather than replaced.
    /// </remarks>
    private static object MaterializeCollection(
        DynamicValueNode node,
        INavigationBase navigation,
        object instance,
        DynamicValueMapper mapper)
    {
        IClrCollectionAccessor accessor = navigation.GetCollectionAccessor()!;
        object collection = accessor.GetOrCreate(instance, forMaterialization: true);

        // Register before filling: this path bypasses FromDynamicValue, so without it the
        // collection's own wire id is never registered and a later back-reference to it
        // dangles.
        mapper.RegisterMaterialized(node.Id, collection);

        foreach (DynamicValueNode item in node.Items ?? [])
        {
            if (mapper.FromDynamicValue(item) is { } related)
            {
                accessor.Add(instance, related, forMaterialization: true);
            }
        }

        return collection;
    }
}
