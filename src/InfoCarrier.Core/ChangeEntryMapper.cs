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

        // Which properties nobody set, but *only where the value cannot say so itself*.
        //
        // The sentinel is computed twice, like everything else the two models each derive — and it
        // diverges, because `HasDefaultValue(true)` makes a `bool`'s sentinel `true` on the server
        // and leaves it `false` here. Naming every unset property would therefore hand the server
        // *this* model's answer to a question its own model answers better: it holds the value and
        // its own sentinel, and comparing them is exactly what EF does. `TrueDefault = false` reads
        // as unset here and as deliberate there, and there is right.
        //
        // What the server cannot recover is a sentinel the wire could not carry. EF reads a `bool`
        // property through a `bool?` field, so an unset one arrives as `false` — indistinguishable
        // from a real `false`, and no longer equal to the sentinel it was. That is the case this
        // names, and the equality test below is what limits it to that case.
        List<string>? sentinel = null;

        foreach (IProperty property in entityType.GetProperties())
        {
            if (modified is not null && entry.IsModified(property))
            {
                modified.Add(property.Name);
            }

            if (!entry.HasExplicitValue(property)
                && !Equals(property.Sentinel, entry.GetCurrentValue(property)))
            {
                (sentinel ??= []).Add(property.Name);
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

            // A foreign key's original as well as a concurrency token's, and for a different
            // consumer: **EF's command ordering**, not the concurrency check (J11).
            //
            // `CommandBatchPreparer` builds its dependency graph from *original* foreign-key
            // values, because that is what says a dependent is **releasing** a principal. The
            // server rebuilds an entity from current values, attaches it and sets `Modified`,
            // which snapshots originals from the entity — so without this every original equals
            // its current value, an orphaned dependent looks like one that never had a parent,
            // nothing orders its `UPDATE` before the principal's `DELETE`, and the store refuses
            // the delete. Measured as **165 `FOREIGN KEY constraint failed`** the moment
            // `ProxyGraphUpdates` reached a store that enforces them (J3); Tier A cannot show it,
            // and a single-context EF never loses the originals in the first place.
            //
            // Foreign keys only. C42 measured the symmetric temptation — sending every propagated
            // foreign key *back* — at **1 fixed, 2 broken**, and the rule it established is what
            // this satisfies: send what the other side cannot derive, and nothing else.
            //
            // **`Deleted` as well as `Modified`, and the sentence that used to stand here was
            // wrong.** It read: *"a `Deleted` entry needs no ordering hint, because the row it
            // releases is the one being deleted"*. That holds while the deleted row is only ever
            // a dependent. It is false the moment **one deleted row is a dependent of another**,
            // and then the missing original is exactly what misorders the batch:
            //
            //   `ManyToManyTrackingTestBase.Can_delete_with_many_to_many` deletes an `EntityOne`
            //   and an `EntityTwo` in one call, and `EntityTwo.CollectionInverseId` points at that
            //   very `EntityOne`. EF's own `ClientSetNull` fixup nulls the FK on the client before
            //   the entry is sent, so the *current* value carries no edge either; the server then
            //   rebuilds the row with a null FK, snapshots originals from it, and
            //   `CommandBatchPreparer` sees nothing to order. It emitted
            //   `DELETE FROM "EntityOnes"` first and the store answered
            //   `SQLite Error 19: 'FOREIGN KEY constraint failed'`.
            //
            // A single-context EF never loses this: the original is the value the row was loaded
            // with. Only a wire does, which is why Tier A could not show it — InMemory enforces no
            // foreign key — and why R35's move to Tier B is what surfaced it.
            if (carriesOriginals && (property.IsConcurrencyToken || property.IsForeignKey()))
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

                // `ToComplexValue`, not `ToDynamicValue`: a complex value is walked reflectively
                // and the CLR type is not the model, so the complex type has to travel with it or
                // an `Ignore`d member goes on the wire (C92).
                Value = mapper.ToComplexValue(entry.GetCurrentValue(complexProperty), complexProperty),
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
            SentinelProperties = sentinel,
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
