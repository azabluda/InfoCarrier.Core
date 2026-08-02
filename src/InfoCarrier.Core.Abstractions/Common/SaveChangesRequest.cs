// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common;

/// <summary>
///     A single change-tracker entry for the SaveChanges pipeline (wire-protocol §2.2).
///     Carries entity identity (type + key), state, modified values, and original
///     concurrency tokens. M2M join entries are included as ordinary entries.
/// </summary>
public sealed record ChangeEntry
{
    /// <summary>
    ///     A client-assigned correlation id (position in the submitted list) used to key
    ///     store-generated values back to this entry (research-findings §9 / wire-protocol W2).
    /// </summary>
    public required int CorrelationId { get; init; }

    /// <summary>
    ///     The EF entity-type name (the distinguishing key for shared-type entities).
    /// </summary>
    public required string EntityTypeName { get; init; }

    /// <summary>
    ///     The CLR type name of the entity (shared-assembly type), for resolution.
    /// </summary>
    public required string ClrTypeName { get; init; }

    /// <summary>
    ///     The entity state: <c>Added</c>, <c>Modified</c>, or <c>Deleted</c>.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    ///     Current property values (including key values), serialized per the entity-mapper
    ///     contract. For <c>Modified</c>, only changed values plus keys are required.
    /// </summary>
    public required byte[] SerializedValues { get; init; }

    /// <summary>
    ///     Original values of concurrency tokens, for optimistic-concurrency checks.
    ///     Null when the entity has no concurrency tokens.
    /// </summary>
    public byte[]? SerializedOriginalValues { get; init; }

    /// <summary>
    ///     Relationships to other entries in the same request, by correlation id.
    /// </summary>
    /// <remarks>
    ///     Foreign keys travel as ordinary property values, but only when they have a value to
    ///     travel. A dependent whose principal is itself new holds a <em>temporary</em> key,
    ///     which must not be sent — it would ask the store to insert a row pointing at a
    ///     made-up id. The relationship is sent instead, and EF's own fixup assigns the real
    ///     foreign key on the server once the principal is inserted (research-findings §9).
    /// </remarks>
    public IReadOnlyList<NavigationLink>? Navigations { get; init; }
}

/// <summary>
///     One navigation of a <see cref="ChangeEntry" />, pointing at other entries in the same
///     request.
/// </summary>
public sealed record NavigationLink
{
    /// <summary>
    ///     The navigation's name on the declaring entity type.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Correlation ids of the related entries. A reference navigation has at most one.
    /// </summary>
    public required IReadOnlyList<int> TargetCorrelationIds { get; init; }
}

/// <summary>
///     A SaveChanges request: the serialized change-tracker entries (wire-protocol §2.2).
/// </summary>
public sealed record SaveChangesRequest
{
    /// <summary>
    ///     The change entries, in submission order. The server replays them in order.
    /// </summary>
    public required IReadOnlyList<ChangeEntry> Entries { get; init; }
}

/// <summary>
///     Store-generated values for one entry, keyed back by <see cref="ChangeEntry.CorrelationId" />.
/// </summary>
public sealed record GeneratedValues
{
    /// <summary>
    ///     The correlation id of the originating <see cref="ChangeEntry" />.
    /// </summary>
    public required int CorrelationId { get; init; }

    /// <summary>
    ///     The store-generated property values (identity keys, computed columns,
    ///     concurrency tokens, defaults), serialized per the entity-mapper contract.
    /// </summary>
    public required byte[] SerializedValues { get; init; }
}

/// <summary>
///     A SaveChanges result: store-generated values per entry (wire-protocol §2.2).
/// </summary>
public sealed record SaveChangesResult
{
    /// <summary>
    ///     The number of state entries persisted.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    ///     Store-generated values, keyed back to entries by correlation id.
    /// </summary>
    public required IReadOnlyList<GeneratedValues> GeneratedValues { get; init; }
}
