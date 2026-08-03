// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common;

/// <summary>
///     The operation discriminated by an <see cref="InfoCarrierEnvelope" /> (wire-protocol §1).
/// </summary>
public enum InfoCarrierOperation
{
    /// <summary>A query operation.</summary>
    Query = 0,

    /// <summary>A SaveChanges operation.</summary>
    SaveChanges = 1,

    /// <summary>Begin a transaction.</summary>
    BeginTransaction = 2,

    /// <summary>Commit a transaction.</summary>
    CommitTransaction = 3,

    /// <summary>Roll back a transaction.</summary>
    RollbackTransaction = 4,

    /// <summary>Create a savepoint inside a transaction.</summary>
    CreateSavepoint = 5,

    /// <summary>Roll a transaction back to a savepoint, leaving it open.</summary>
    RollbackToSavepoint = 6,

    /// <summary>Release a savepoint, keeping the work done since it.</summary>
    ReleaseSavepoint = 7,

    /// <summary>Ask whether the server's store supports savepoints at all.</summary>
    SupportsSavepoints = 8,
}
