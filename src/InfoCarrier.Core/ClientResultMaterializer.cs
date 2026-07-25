// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core;

/// <summary>
///     Materializes query results on the client (requirements §2.5): deserializes rows,
///     resolves identity against the client change tracker (reuse tracked instance / attach
///     new), populates scalar properties, and wires navigation fixup. Non-entity projections
///     are materialized from columnar data (E2).
/// </summary>
public class ClientResultMaterializer
{
    private readonly DbContext _context;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClientResultMaterializer" /> class.
    /// </summary>
    public ClientResultMaterializer(DbContext context)
        => _context = context;

    /// <summary>
    ///     Materializes the wire result into a sequence of <typeparamref name="TElement" />.
    /// </summary>
    public IEnumerable<TElement> Materialize<TElement>(QueryDataResult result)
    {
        foreach (object? row in DeserializeRows<TElement>(result))
        {
            if (row is null)
            {
                continue;
            }

            yield return result.IsEntityResult
                ? MaterializeEntity<TElement>(row)
                : (TElement)row; // Projection: row is already the typed projection (E2).
        }
    }

    private IEnumerable<object?> DeserializeRows<TElement>(QueryDataResult result)
    {
        // Entity results: deserialize each row to its runtime entity type for identity resolution.
        // Projection results: deserialize directly to TElement (anonymous/DTO/value-tuple) —
        // the server returned columnar data shaped like the projection (requirements §3.2).
        Type rowType = result.IsEntityResult
            ? ResolveRowType(result) ?? typeof(TElement)
            : typeof(TElement);

        Type listType = typeof(List<>).MakeGenericType(rowType);
        object? deserialized = System.Text.Json.JsonSerializer.Deserialize(result.SerializedResults, listType);
        return deserialized as IEnumerable<object?> ?? [];
    }

    private Type? ResolveRowType(QueryDataResult result)
        // Resolve the entity CLR type from the model by the element-type name carried on the wire.
        => result.ElementTypeName is null
            ? null
            : _context.Model.GetEntityTypes()
                .FirstOrDefault(e => e.ClrType.FullName == result.ElementTypeName || e.Name == result.ElementTypeName)
                ?.ClrType;

    private TElement MaterializeEntity<TElement>(object row)
    {
        // Identity resolution: if an entity with the same key is already tracked, reuse it;
        // otherwise attach the new instance (requirements §2.5 step 2).
        IEntityType? entityType = _context.Model.FindEntityType(row.GetType());
        if (entityType?.FindPrimaryKey() is { } key)
        {
            object?[] keyValues = key.Properties
                .Select(p => p.GetGetter().GetClrValue(row))
                .ToArray();

            EntityEntry? tracked = _context.ChangeTracker.Entries()
                .FirstOrDefault(e => e.Entity.GetType() == row.GetType()
                    && KeyEquals(e, key, keyValues));

            if (tracked is not null)
            {
                return (TElement)tracked.Entity; // Reuse tracked instance.
            }
        }

        // Attach the new instance as Unchanged; EF fixup wires navigations from FK relationships.
        // Setting the state explicitly (rather than Attach) avoids the client provider's
        // store-generated-key value generation — the server owns key generation (requirements §2.2).
        EntityEntry entry = _context.Entry(row);
        entry.State = EntityState.Unchanged;
        return (TElement)entry.Entity;
    }

    private static bool KeyEquals(EntityEntry entry, IKey key, object?[] keyValues)
    {
        for (int i = 0; i < key.Properties.Count; i++)
        {
            object? current = entry.Property(key.Properties[i].Name).CurrentValue;
            if (!Equals(current, keyValues[i]))
            {
                return false;
            }
        }

        return true;
    }
}
