// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.Common;

/// <summary>
///     A query request: the serialized expression tree plus execution context
///     (wire-protocol §2.1). The expression tree is the raw captured LINQ tree
///     (ADR-006 raw capture), serialized per expression-serialization §3.
/// </summary>
public sealed record QueryDataRequest : IInfoCarrierRequest
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

    /// <summary>
    ///     The open server transaction this query belongs to, or <see langword="null" /> to run
    ///     on its own (wire-protocol W3).
    /// </summary>
    /// <remarks>
    ///     The token from <see cref="TransactionResult.TransactionId" />. A transport may be
    ///     stateless and a server may serve many clients, so an open transaction cannot be
    ///     implied by the connection: it has to be named on every request that belongs to it.
    /// </remarks>
    public string? TransactionId { get; init; }

    /// <summary>
    ///     What the query asked of the backing store's split-query behaviour, or
    ///     <see langword="null" /> when it asked for nothing.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The hint travels beside the tree rather than inside it, and that is measured
    ///         rather than chosen.</b> <c>AsSplitQuery</c> and <c>AsSingleQuery</c> are stripped
    ///         from the captured tree before the boundary is drawn: left in place, the hint lands
    ///         in the CLIENT residual, where EF's own method returns its source untouched because
    ///         the provider is not an <c>EntityQueryProvider</c>, and on a nested query root it
    ///         forces the cut below that root and strands the navigations above it. Leaving them
    ///         in measured 236 failures of 326 across the three split-query specification classes.
    ///     </para>
    ///     <para>
    ///         <b>None of that says the server should not be told.</b> The server is the half with
    ///         a relational provider, so it is the half that can honour the hint, and this is how
    ///         it hears about it. <c>ServerQueryExecutor</c> re-applies it to the rebuilt query.
    ///     </para>
    ///     <para>
    ///         <b>Optional on purpose.</b> A request from an older client omits it and the server
    ///         reads <see langword="null" />, which is exactly the behaviour that client had.
    ///     </para>
    /// </remarks>
    public QuerySplittingBehavior? SplitQueryBehavior { get; init; }
}
