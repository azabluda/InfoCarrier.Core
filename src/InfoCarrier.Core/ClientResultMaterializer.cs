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

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClientResultMaterializer" /> class.
    /// </summary>
    public ClientResultMaterializer(
        DbContext context,
        IExpressionSerializer serializer,
        QueryTrackingBehavior trackingBehavior)
    {
        _context = context;
        _stateManager = context.GetService<IStateManager>();
        _serializer = (ExpressionSerializer)serializer;
        _trackingBehavior = trackingBehavior;
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
        if (pk is null || _trackingBehavior != QueryTrackingBehavior.TrackAll)
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

        object instance = Activator.CreateInstance(entityType.ClrType, nonPublic: true)!;

        // Attach as Unchanged via the internal state manager — bypasses public-API value
        // generation for store-generated keys (the server owns key generation).
        InternalEntityEntry entry = _stateManager.GetOrCreateEntry(instance, entityType);
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

        entry.SetEntityState(EntityState.Unchanged);
        PopulateNavigations(row, instance, entityType, entry, mapper);
        return instance;
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
