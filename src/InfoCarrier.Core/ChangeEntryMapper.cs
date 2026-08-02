// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;

namespace InfoCarrier.Core;

/// <summary>
///     Translates change-tracker entries to and from the wire (wire-protocol §2.2,
///     research-findings §9).
/// </summary>
/// <remarks>
///     <para>
///         An entry travels as its <em>property values</em>, not as an object graph. The server
///         rebuilds an instance, sets those values, and hands it to a real change tracker; EF then
///         does the ordering, the fixup and the store round trip. Nothing here reimplements any of
///         that.
///     </para>
///     <para>
///         Each entry carries a correlation id — its position in the submitted list — and
///         store-generated values come back keyed by the same id. That is what bridges the
///         client's temporary key and the server's real one (research-findings §9), and it is
///         why the server must replay entries in the order they arrive.
///     </para>
/// </remarks>
public static class ChangeEntryMapper
{
    /// <summary>
    ///     Captures one client change-tracker entry for transmission.
    /// </summary>
    /// <remarks>
    ///     A temporary value is deliberately omitted: it is the client's placeholder for a key the
    ///     store has not generated yet, and sending it would ask the server to insert a row with a
    ///     made-up primary key.
    /// </remarks>
    public static ChangeEntry ToChangeEntry(
        IUpdateEntry entry,
        int correlationId,
        DynamicValueMapper mapper,
        IReadOnlyDictionary<object, int>? correlationByEntity = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(mapper);

        IEntityType entityType = (IEntityType)entry.EntityType;
        var properties = new List<DynamicPropertyValue>();

        foreach (IProperty property in entityType.GetProperties())
        {
            if (entry.EntityState == EntityState.Added && entry.HasTemporaryValue(property))
            {
                continue;
            }

            properties.Add(new DynamicPropertyValue
            {
                Name = property.Name,
                Value = mapper.ToDynamicValue(entry.GetCurrentValue(property), property.ClrType),
            });
        }

        return new ChangeEntry
        {
            CorrelationId = correlationId,
            EntityTypeName = entityType.Name,
            ClrTypeName = entityType.ClrType.FullName ?? entityType.ClrType.Name,
            State = entry.EntityState.ToString(),
            SerializedValues = Serialize(entityType, properties, mapper),
            Navigations = correlationByEntity is null
                ? null
                : ReadNavigations(entry, entityType, correlationByEntity),
        };
    }

    /// <summary>
    ///     Records which other entries in the same request this one is related to.
    /// </summary>
    /// <remarks>
    ///     Only entries that are themselves part of the request. A navigation to an entity the
    ///     server already has needs no link: its foreign key is a real value and travelled as an
    ///     ordinary property.
    /// </remarks>
    private static IReadOnlyList<NavigationLink>? ReadNavigations(
        IUpdateEntry entry,
        IEntityType entityType,
        IReadOnlyDictionary<object, int> correlationByEntity)
    {
        EntityEntry entityEntry = entry.ToEntityEntry();
        List<NavigationLink>? links = null;

        foreach (INavigationBase navigation in entityType.GetNavigations().Cast<INavigationBase>()
                     .Concat(entityType.GetSkipNavigations()))
        {
            object? value = navigation.IsCollection
                ? entityEntry.Collection(navigation.Name).CurrentValue
                : entityEntry.Reference(navigation.Name).CurrentValue;

            if (value is null)
            {
                continue;
            }

            List<int> targets = [];
            if (navigation.IsCollection)
            {
                foreach (object? related in (System.Collections.IEnumerable)value)
                {
                    if (related is not null && correlationByEntity.TryGetValue(related, out int id))
                    {
                        targets.Add(id);
                    }
                }
            }
            else if (correlationByEntity.TryGetValue(value, out int id))
            {
                targets.Add(id);
            }

            if (targets.Count > 0)
            {
                (links ??= []).Add(new NavigationLink { Name = navigation.Name, TargetCorrelationIds = targets });
            }
        }

        return links;
    }

    /// <summary>
    ///     Reads the property values an entry carried.
    /// </summary>
    public static IReadOnlyList<DynamicPropertyValue> ReadValues(byte[] serialized)
        => JsonSerializer.Deserialize(serialized, ExpressionJsonContext.Default.DynamicValueNode)?.Properties
            ?? [];

    /// <summary>
    ///     Serializes store-generated values to return to the client.
    /// </summary>
    public static byte[] Serialize(
        IEntityType entityType,
        IReadOnlyList<DynamicPropertyValue> properties,
        DynamicValueMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        var node = new DynamicValueNode
        {
            Type = mapper.TypeMapper.ToTypeNode(entityType.ClrType),
            Properties = properties,
        };

        return JsonSerializer.SerializeToUtf8Bytes(node, ExpressionJsonContext.Default.DynamicValueNode);
    }
}
