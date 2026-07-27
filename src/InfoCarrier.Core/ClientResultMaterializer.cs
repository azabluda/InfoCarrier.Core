// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using InfoCarrier.Core.Common;
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

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClientResultMaterializer" /> class.
    /// </summary>
    public ClientResultMaterializer(DbContext context)
    {
        _context = context;
        _stateManager = context.GetService<IStateManager>();
    }

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
        IEntityType? entityType = _context.Model.FindEntityType(row.GetType());
        if (entityType is null)
        {
            return (TElement)row;
        }

        // Identity resolution via EF's own identity map (v1 pattern): reuse the tracked
        // instance if an entity with the same key is already tracked.
        IKey? pk = entityType.FindPrimaryKey();
        if (pk is not null)
        {
            object?[] keyValues = pk.Properties
                .Select(p => p.GetGetter().GetClrValue(row))
                .ToArray();

            InternalEntityEntry? tracked = _stateManager.TryGetEntry(pk, keyValues);
            if (tracked is not null)
            {
                return (TElement)tracked.Entity;
            }
        }

        // Attach as Unchanged via the internal state manager — bypasses public-API value
        // generation for store-generated keys (the server owns key generation).
        InternalEntityEntry entry = _stateManager.GetOrCreateEntry(row, entityType);
        entry.SetEntityState(EntityState.Unchanged);
        return (TElement)entry.Entity;
    }
}
