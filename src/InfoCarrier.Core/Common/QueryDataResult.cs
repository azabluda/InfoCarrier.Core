// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core.Common;

/// <summary>
///     A query result: rows as they arrive, plus the metadata needed for client-side
///     materialization, identity resolution, and projection (wire-protocol §2.1).
/// </summary>
/// <remarks>
///     <para>
///         For entity types, rows are identity-keyed with loaded-navigation markers so the
///         client can resolve identity and fix up navigations (requirements §2.5). For
///         non-entity projections, the server returns type-agnostic columnar data and the
///         client applies the final projection locally (requirements §3.2).
///     </para>
///     <para>
///         <b><see cref="Rows" /> used to be <c>byte[] SerializedResults</c> — the whole result
///         set as one blob — and that was D7's buffering point 2.</b> It was two base64 layers
///         deep on the wire (a <c>byte[]</c> inside <see cref="QueryDataResult" /> inside
///         <see cref="InfoCarrierEnvelope.Payload" />), which nothing can stream through, so this
///         record is no longer an envelope payload at all: a query response is a
///         <see cref="QueryStreamItem" /> array and this is what the client's transport builds
///         from it.
///     </para>
///     <para>
///         <b>The sequence is live, and whoever takes one owns it.</b> On the server side it holds
///         a <c>DbContext</c> — and, inside a transaction, a store connection — open until it is
///         enumerated to the end or its enumerator is disposed. A caller that stops early must
///         dispose the enumerator, which <c>await foreach</c> does; a caller that never enumerates
///         at all leaks the context. That is the resource note D7 records, and it is the reason the
///         one consumer in this provider —
///         <see cref="ClientResultMaterializer.MaterializeAsync{TElement}" /> — drains it.
///     </para>
/// </remarks>
public sealed record QueryDataResult
{
    /// <summary>
    ///     The result rows, produced as the server reads them. The shape of a row is defined by
    ///     the result-mapping contract (identity-keyed entity rows, or columnar projection rows).
    /// </summary>
    public required IAsyncEnumerable<DynamicValueNode> Rows { get; init; }

    /// <summary>
    ///     Whether the result element type is an entity type the server's model knows.
    ///     When false, the client applies the final projection locally.
    /// </summary>
    public required bool IsEntityResult { get; init; }

    /// <summary>
    ///     The element type name, for diagnostics and projection routing.
    /// </summary>
    public string? ElementTypeName { get; init; }
}
