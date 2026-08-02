// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
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
    ///     A temporary value travels, listed in <see cref="ChangeEntry.TemporaryProperties" /> so
    ///     the server can mark it temporary too. It is meaningless to the store, but a principal
    ///     and its dependents share it, which is what identifies the relationship between them.
    /// </remarks>
    public static ChangeEntry ToChangeEntry(IUpdateEntry entry, int correlationId, DynamicValueMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(mapper);

        IEntityType entityType = (IEntityType)entry.EntityType;
        var properties = new List<DynamicPropertyValue>();

        List<string>? temporary = null;

        foreach (IProperty property in entityType.GetProperties())
        {
            if (entry.HasTemporaryValue(property))
            {
                // Sent, and flagged. The value is meaningless to the store, but a principal and
                // its dependents share it, so it is what identifies the relationship; the server
                // marks it temporary too and EF replaces every occurrence with the real key.
                //
                // Flagged whatever the state. A placeholder reaches an *existing* row whenever
                // one is reparented onto a new principal — `old1.RootId = newRoot.Id` — and that
                // entry is `Modified`. Restricting this to `Added` left the server unable to tell
                // that FK from a real one, so it stored a placeholder as though it were a key.
                (temporary ??= []).Add(property.Name);
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
            TemporaryProperties = temporary,
        };
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
