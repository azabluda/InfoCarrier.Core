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

        // An `Added` entry has no original — nothing existed to conflict with — and EF answers
        // `GetOriginalValue` with the current one there anyway.
        bool carriesOriginals = entry.EntityState is EntityState.Modified or EntityState.Deleted;
        List<DynamicPropertyValue>? originals = null;

        // Only a `Modified` entry has a meaningful answer: on an `Added` one EF reports every
        // property modified, and on a `Deleted` one none, and neither is information the server
        // needs. Collected as the loop goes so the whole set is in hand before the request is
        // built.
        List<string>? modified = entry.EntityState == EntityState.Modified ? [] : null;

        foreach (IProperty property in entityType.GetProperties())
        {
            if (modified is not null && entry.IsModified(property))
            {
                modified.Add(property.Name);
            }

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
                // The *provider* value, so a value converter is honoured rather than merely
                // gone through. The CLR value reached the mapper's reflective member walk
                // instead, and `IPAddress.ScopeId` throws `SocketException` for an IPv4 address
                // (`ValueConvertersEndToEndTestBase`).
                Value = mapper.ToDynamicValue(
                    Expressions.PrimitiveCoercion.ToWireValue(property, entry.GetCurrentValue(property)),
                    Expressions.PrimitiveCoercion.WireType(property)),
            });

            if (carriesOriginals && property.IsConcurrencyToken)
            {
                // The value the check is made against. Only the token's original matters: the
                // server rebuilds the entity from the *current* values, attaches it and sets
                // `Modified`, so every original it has equals its current one by construction —
                // and a client that bumps its own token would then be checked against the value
                // it had just written, refusing a write nobody conflicted with.
                //
                // **After the current value, and that ordering is load-bearing.** A `byte[]` token
                // is not a wire primitive, so it travels as a referenceable object: the first
                // mapping of an instance defines it, every later one is a back-reference. When the
                // token has *not* been changed both values are the same array — which is the whole
                // point of `..._original_value_matches_does_not_throw` — so one of the two is a
                // `Ref`. The two payloads are decoded independently and `SerializedValues` is
                // decoded first, so the definition has to be there. Mapping the original first put
                // it in `SerializedOriginalValues` and the current values arrived holding a
                // reference to a value nobody had materialized yet: "Dangling wire reference 1".
                (originals ??= []).Add(new DynamicPropertyValue
                {
                    Name = property.Name,
                    Value = mapper.ToDynamicValue(
                        Expressions.PrimitiveCoercion.ToWireValue(property, entry.GetOriginalValue(property)),
                        Expressions.PrimitiveCoercion.WireType(property)),
                });
            }
        }

        // Complex properties are not in `GetProperties()`. Without this a saved entity arrived
        // with every complex member null, and EF answered "Required properties {'Species',
        // 'Species'} are missing" — which also names the reason they cannot be flattened into
        // the loop above: two complex leaves share a name.
        foreach (IComplexProperty complexProperty in entityType.GetComplexProperties())
        {
            properties.Add(new DynamicPropertyValue
            {
                Name = complexProperty.Name,
                Value = mapper.ToDynamicValue(
                    entry.GetCurrentValue(complexProperty), complexProperty.ClrType),
            });
        }

        return new ChangeEntry
        {
            CorrelationId = correlationId,
            EntityTypeName = entityType.Name,
            ClrTypeName = entityType.ClrType.FullName ?? entityType.ClrType.Name,
            State = entry.EntityState.ToString(),
            SerializedValues = Serialize(entityType, properties, mapper),
            SerializedOriginalValues = originals is null ? null : Serialize(entityType, originals, mapper),
            TemporaryProperties = temporary,
            ModifiedProperties = modified,
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
