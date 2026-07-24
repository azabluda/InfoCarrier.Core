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
        object? deserialized = Deserialize(result.SerializedResults);
        if (deserialized is not IEnumerable rows)
        {
            yield break;
        }

        foreach (object? row in rows)
        {
            if (row is null)
            {
                continue;
            }

            yield return result.IsEntityResult
                ? MaterializeEntity<TElement>(row)
                : MaterializeProjection<TElement>(row);
        }
    }

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

        // Attach the new instance; EF fixup wires navigations from FK relationships.
        EntityEntry entry = _context.Attach(row);
        return (TElement)entry.Entity;
    }

    private TElement MaterializeProjection<TElement>(object row)
        // Non-entity projections materialize directly from the row (E2 refines columnar mapping).
        => (TElement)row;

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

    private static object? Deserialize(byte[] payload)
        => System.Text.Json.JsonSerializer.Deserialize<object>(payload);
}
