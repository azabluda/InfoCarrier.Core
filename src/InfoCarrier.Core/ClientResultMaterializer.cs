// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

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
        mapper.EntityMaterializer = node => MaterializeEntity(node, mapper);

        foreach (DynamicValueNode row in DeserializeRows(result))
        {
            // A null row is data, not an absent row. `Select(c => c.Orders.FirstOrDefault())`
            // over a customer with no orders produces one, and skipping it silently returned
            // 89 rows where the query defined 91.
            object? value = mapper.FromDynamicValue(row);

            yield return value is null ? default! : (TElement)value;
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

        object instance = Activator.CreateInstance(entityType.ClrType, nonPublic: true)!;

        // Attach as Unchanged via the internal state manager — bypasses public-API value
        // generation for store-generated keys (the server owns key generation).
        InternalEntityEntry entry = _stateManager.GetOrCreateEntry(instance, entityType);

        if (deferredKey is not null)
        {
            _deferredIdentity[(entityType, deferredKey)] = (instance, entry);
        }
        mapper.RegisterMaterialized(row.Id, instance);

        foreach (DynamicPropertyValue member in row.Properties)
        {
            if (member.IsLoadedNavigation)
            {
                continue;
            }

            IProperty? property = entityType.FindProperty(member.Name);
            if (property is null)
            {
                continue;
            }

            // Through the entry, so shadow properties land too.
            entry[property] = member.Value is not null
                ? mapper.FromDynamicValue(member.Value)
                : PrimitiveCoercion.Coerce(member.PrimitiveValue, property.ClrType);
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
        object instance = Activator.CreateInstance(entityType.ClrType, nonPublic: true)!;
        mapper.RegisterMaterialized(row.Id, instance);

        foreach (DynamicPropertyValue member in row.Properties)
        {
            if (member.IsLoadedNavigation)
            {
                continue;
            }

            if (entityType.FindProperty(member.Name) is not { } property
                || property.PropertyInfo is not { CanWrite: true } clrProperty)
            {
                continue;
            }

            clrProperty.SetValue(instance, member.Value is not null
                ? mapper.FromDynamicValue(member.Value)
                : PrimitiveCoercion.Coerce(member.PrimitiveValue, property.ClrType));
        }

        PopulateNavigations(row, instance, entityType, entry: null, mapper);
        return instance;
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
                : mapper.FromDynamicValue(member.Value);

            if (related is not null && navigation.PropertyInfo is { CanWrite: true } property)
            {
                property.SetValue(instance, related);
            }

            // Distinguishes a loaded-but-empty navigation from an unloaded one
            // (requirements §2.5 step 5).
            entry?.SetIsLoaded(navigation);
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
