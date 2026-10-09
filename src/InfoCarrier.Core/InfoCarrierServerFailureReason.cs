// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core;

/// <summary>Stable, bounded classifications for server diagnostics.</summary>
public enum InfoCarrierServerFailureReason
{
    /// <summary>Invalid Payload.</summary>
    InvalidPayload = 0,
    /// <summary>Payload Limit.</summary>
    PayloadLimit = 1,
    /// <summary>Invalid Descriptor.</summary>
    InvalidDescriptor = 2,
    /// <summary>Permission.</summary>
    Permission = 3,
    /// <summary>Configuration.</summary>
    Configuration = 4,
    /// <summary>Protocol Version.</summary>
    ProtocolVersion = 5,
    /// <summary>Unknown Operation.</summary>
    UnknownOperation = 6,
    /// <summary>Transaction Not Open.</summary>
    TransactionNotOpen = 7,
    /// <summary>Wrong Instance.</summary>
    WrongInstance = 8,
    /// <summary>Caller Mismatch.</summary>
    CallerMismatch = 9,
    /// <summary>Concurrency.</summary>
    Concurrency = 10,
    /// <summary>Database.</summary>
    Database = 11,
    /// <summary>Rebinding Failure.</summary>
    RebindingFailure = 12,
    /// <summary>Execution Failure.</summary>
    ExecutionFailure = 13,
    /// <summary>Result Mapping Failure.</summary>
    ResultMappingFailure = 14,
    /// <summary>Response Serialization.</summary>
    ResponseSerialization = 15,
    /// <summary>Cancellation.</summary>
    Cancellation = 16,
    /// <summary>Unexpected Cancellation.</summary>
    UnexpectedCancellation = 17,
    /// <summary>Response Write.</summary>
    ResponseWrite = 18,
    /// <summary>Cleanup Rollback Failure.</summary>
    CleanupRollbackFailure = 19,
    /// <summary>Cleanup Transaction Disposal Failure.</summary>
    CleanupTransactionDisposalFailure = 20,
    /// <summary>Cleanup Scope Disposal Failure.</summary>
    CleanupScopeDisposalFailure = 21,
    /// <summary>Transaction Evicted.</summary>
    TransactionEvicted = 22,
    /// <summary>Cleanup Completed.</summary>
    CleanupCompleted = 23,
}

/// <summary>Bounded phases for request diagnostics.</summary>
public enum InfoCarrierServerPhase
{
    /// <summary>Unknown.</summary>
    Unknown = 0,
    /// <summary>Input.</summary>
    Input = 1,
    /// <summary>Validation.</summary>
    Validation = 2,
    /// <summary>Rebinding.</summary>
    Rebinding = 3,
    /// <summary>Execution.</summary>
    Execution = 4,
    /// <summary>Result Mapping.</summary>
    ResultMapping = 5,
    /// <summary>Result Serialization.</summary>
    ResultSerialization = 6,
    /// <summary>Fault Serialization.</summary>
    FaultSerialization = 7,
    /// <summary>Envelope Serialization.</summary>
    EnvelopeSerialization = 8,
    /// <summary>Response Write.</summary>
    ResponseWrite = 9,
}
