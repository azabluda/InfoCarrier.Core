// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.Common;

/// <summary>
///     A query request: the serialized expression tree plus execution context
///     (wire-protocol §2.1). The expression tree is the raw captured LINQ tree
///     (ADR-006 raw capture), serialized per expression-serialization §3.
/// </summary>
public sealed record QueryDataRequest
{
    /// <summary>
    ///     The serialized expression tree payload (the serialized query expression DTO).
    ///     Produced by the client's expression serializer; consumed by the server's
    ///     rebind-and-execute pipeline.
    /// </summary>
    public required byte[] SerializedQuery { get; init; }

    /// <summary>
    ///     The <see cref="QueryTrackingBehavior" /> of the client-side context, which the
    ///     server-side context must match.
    /// </summary>
    public required QueryTrackingBehavior TrackingBehavior { get; init; }

    /// <summary>
    ///     Whether this is an async query.
    /// </summary>
    public required bool IsAsync { get; init; }

    /// <summary>
    ///     Whether the query returns a single result (vs a sequence).
    /// </summary>
    public required bool ReturnsSingleResult { get; init; }
}
