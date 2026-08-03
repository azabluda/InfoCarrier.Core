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
public class ClientResultMaterializer
{
    private readonly DbContext _context;
    private readonly IStateManager _stateManager;
    private readonly ExpressionSerializer _serializer;
    private readonly QueryTrackingBehavior _trackingBehavior;
    private readonly bool _deferTracking;

    // Identity map for entities materialized but not yet attached. The state manager's own map
    // only holds tracked entries, so while tracking is deferred it cannot answer "have I already
    // built this one?" — and two rows naming the same customer would become two instances.
    private readonly Dictionary<(IEntityType Type, string Key), (object Instance, InternalEntityEntry Entry)> _deferredIdentity = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClientResultMaterializer" /> class.
    /// </summary>
    public ClientResultMaterializer(
        DbContext context,
        IExpressionSerializer serializer,
        QueryTrackingBehavior trackingBehavior,
        bool deferTracking = false)
    {
        _context = context;
        _stateManager = context.GetService<IStateManager>();
        _serializer = (ExpressionSerializer)serializer;
        _trackingBehavior = trackingBehavior;
        _deferTracking = deferTracking;
    }

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
    public IReadOnlyDictionary<object, InternalEntityEntry> Deferred => _deferred;

    private readonly Dictionary<object, InternalEntityEntry> _deferred = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     Attaches one deferred entity, if it is one this materializer built.
    /// </summary>
    public void AttachIfDeferred(object entity)
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
    public IEnumerable<TElement> Materialize<TElement>(QueryDataResult result)
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
        }
        finally
        {
            mapper.EntityMaterializer = outer;
        }

        return rows;
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
                .Select((p, i) => PrimitiveCoercion.Coerce(row.EntityKey.KeyValues[i], p.ClrType))
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
        InternalEntityEntry entry = _stateManager.CreateEntry(ReadPrimitives(row, entityType), entityType);
        object instance = entry.Entity;

        if (deferredKey is not null)
        {
            _deferredIdentity[(entityType, deferredKey)] = (instance, entry);
        }
        mapper.RegisterMaterialized(row.Id, instance);

        // Through the entry, so shadow properties land too.
        foreach ((IProperty property, DynamicValueNode node) in ComplexScalars(row, entityType))
        {
            entry[property] = mapper.FromDynamicValue(node);
        }

        ClearPlaceholderReferencesBlockingFixup(entry, entityType);

        if (_deferTracking)
        {
            // Left Detached, so it is invisible to ChangeTracker.Entries() until the residual
            // shows it actually reached the result.
            _deferred[instance] = entry;
        }
        else
        {
            entry.SetEntityState(EntityState.Unchanged);
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
    private void ClearPlaceholderReferencesBlockingFixup(InternalEntityEntry entry, IEntityType entityType)
    {
        foreach (IForeignKey foreignKey in entityType.GetForeignKeys())
        {
            if (foreignKey.DependentToPrincipal is not { PropertyInfo: { CanWrite: true } clrProperty }
                || clrProperty.GetValue(entry.Entity) is null)
            {
                continue;
            }

            object?[] keyValues = foreignKey.Properties.Select(p => entry[p]).ToArray();
            if (Array.Exists(keyValues, v => v is null))
            {
                continue;
            }

            if (_stateManager.TryGetEntry(foreignKey.PrincipalKey, keyValues) is not null)
            {
                clrProperty.SetValue(entry.Entity, null);
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
                clrProperty.SetValue(instance, mapper.FromDynamicValue(node));
            }
        }

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
    /// </remarks>
    private static void SetIsLoadedUntracked(object instance, INavigationBase navigation)
    {
        IServiceProperty? loaderProperty = navigation.DeclaringEntityType
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

        return runtimeType.GetOrCreateMaterializer(_stateManager.EntityMaterializerSource)(
            new MaterializationContext(new ValueBuffer(buffer), _context));
    }

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
                values[property.Name] = PrimitiveCoercion.Coerce(member.PrimitiveValue, property.ClrType);
            }
        }

        return values;
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
            if (!member.IsLoadedNavigation || member.Value is null)
            {
                continue;
            }

            INavigationBase? navigation = entityType.FindNavigation(member.Name)
                ?? (INavigationBase?)entityType.FindSkipNavigation(member.Name);
            if (navigation is null)
            {
                continue;
            }

            object? related = navigation.IsCollection
                ? MaterializeCollection(member.Value, navigation, mapper)
                : ResolveAgainstTracker(mapper.FromDynamicValue(member.Value), navigation, entry);

            if (related is not null && navigation.PropertyInfo is { CanWrite: true } property)
            {
                property.SetValue(instance, related);
            }

            // Distinguishes a loaded-but-empty navigation from an unloaded one
            // (requirements §2.5 step 5).
            if (entry is not null)
            {
                entry.SetIsLoaded(navigation);
            }
            else
            {
                SetIsLoadedUntracked(instance, navigation);
            }
        }
    }

    private object MaterializeCollection(DynamicValueNode node, INavigationBase navigation, DynamicValueMapper mapper)
    {
        // Typed to the target entity, so assigning to ICollection<T> works.
        var items = (System.Collections.IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(navigation.TargetEntityType.ClrType))!;

        // Register before filling: this path bypasses FromDynamicValue, so without it the
        // collection's own wire id is never registered and a later back-reference to it
        // dangles.
        mapper.RegisterMaterialized(node.Id, items);

        foreach (DynamicValueNode item in node.Items ?? [])
        {
            items.Add(mapper.FromDynamicValue(item));
        }

        return items;
    }
}
