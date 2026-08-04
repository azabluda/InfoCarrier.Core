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
    ///     Names of properties whose value is <em>temporary</em> — the client's placeholder for a
    ///     key the store has not generated yet.
    /// </summary>
    /// <remarks>
    ///     These values do travel, and the server marks them temporary on its own entries. That
    ///     is what makes EF's ordinary machinery connect the graph: a principal and its dependents
    ///     share the same temporary key value, and EF replaces every occurrence with the real one
    ///     during <c>SaveChanges</c>. It is also the only thing that works for a many-to-many join
    ///     entity, which has foreign keys but no navigations to link through.
    /// </remarks>
    public IReadOnlyList<string>? TemporaryProperties { get; init; }

    /// <summary>
    ///     Names of the properties a <c>Modified</c> entry actually changed, or
    ///     <see langword="null" /> when the whole row is being written.
    /// </summary>
    /// <remarks>
    ///     A <em>partial</em> update is an ordinary EF operation: attach a stub carrying only the
    ///     key, set one property, mark that one modified, save. Which properties are modified is
    ///     change-tracker state and does not follow from the values — every property's value is on
    ///     the wire either way — so it has to be named. Without it the server set
    ///     <c>State = Modified</c>, EF marked *every* property modified, and the row's untouched
    ///     columns were written from the stub: `Save_partial_update` watched "Apple Cider" become
    ///     <see langword="null" />.
    /// </remarks>
    public IReadOnlyList<string>? ModifiedProperties { get; init; }
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

    /// <summary>
    ///     The open server transaction this save belongs to, or <see langword="null" /> to run
    ///     on its own (wire-protocol W3).
    /// </summary>
    /// <remarks>
    ///     The token from <see cref="TransactionResult.TransactionId" />. A transport may be
    ///     stateless and a server may serve many clients, so an open transaction cannot be
    ///     implied by the connection: it has to be named on every request that belongs to it.
    /// </remarks>
    public string? TransactionId { get; init; }
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
