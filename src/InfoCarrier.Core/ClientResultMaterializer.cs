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

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClientResultMaterializer" /> class.
    /// </summary>
    public ClientResultMaterializer(DbContext context, IExpressionSerializer serializer)
    {
        _context = context;
        _stateManager = context.GetService<IStateManager>();
        _serializer = (ExpressionSerializer)serializer;
    }

    /// <summary>
    ///     Materializes the wire result into a sequence of <typeparamref name="TElement" />.
    /// </summary>
    public IEnumerable<TElement> Materialize<TElement>(QueryDataResult result)
    {
        var mapper = (DynamicValueMapper)_serializer.ValueMapper;

        foreach (DynamicValueNode row in DeserializeRows(result))
        {
            object? value = row.EntityKey is not null
                ? MaterializeEntity(row, mapper)
                : mapper.FromDynamicValue(row);

            if (value is null)
            {
                continue;
            }

            yield return (TElement)value;
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
            return mapper.FromDynamicValue(row);
        }

        // Identity resolution via EF's own identity map (v1 pattern): reuse the tracked
        // instance if an entity with the same key is already tracked.
        IKey? pk = entityType.FindPrimaryKey();
        if (pk is not null && row.EntityKey.KeyValues.Count == pk.Properties.Count)
        {
            object?[] keyValues = pk.Properties
                .Select((p, i) => PrimitiveCoercion.Coerce(row.EntityKey.KeyValues[i], p.ClrType))
                .ToArray();

            InternalEntityEntry? tracked = _stateManager.TryGetEntry(pk, keyValues);
            if (tracked is not null)
            {
                mapper.RegisterMaterialized(row.Id, tracked.Entity);
                PopulateNavigations(row, tracked, mapper);
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
        PopulateNavigations(row, entry, mapper);
        return instance;
    }

    private void PopulateNavigations(DynamicValueNode row, InternalEntityEntry entry, DynamicValueMapper mapper)
    {
        foreach (DynamicPropertyValue member in row.Properties)
        {
            if (!member.IsLoadedNavigation || member.Value is null)
            {
                continue;
            }

            INavigationBase? navigation = entry.EntityType.FindNavigation(member.Name)
                ?? (INavigationBase?)entry.EntityType.FindSkipNavigation(member.Name);
            if (navigation is null)
            {
                continue;
            }

            object? related = navigation.IsCollection
                ? MaterializeCollection(member.Value, navigation, mapper)
                : MaterializeRelated(member.Value, mapper);

            if (related is not null && navigation.PropertyInfo is { CanWrite: true } property)
            {
                property.SetValue(entry.Entity, related);
            }

            // Distinguishes a loaded-but-empty navigation from an unloaded one
            // (requirements §2.5 step 5).
            entry.SetIsLoaded(navigation);
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
            items.Add(MaterializeRelated(item, mapper));
        }

        return items;
    }

    private object? MaterializeRelated(DynamicValueNode node, DynamicValueMapper mapper)
        => node.EntityKey is not null
            ? MaterializeEntity(node, mapper)
            : mapper.FromDynamicValue(node);
}
