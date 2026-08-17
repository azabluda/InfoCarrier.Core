// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core.Common;

/// <summary>
///     One item of a streamed query response (wire-protocol §2.1,
///     <c>docs/architecture.md</c> §6a <b>D7</b>).
/// </summary>
/// <remarks>
///     <para>
///         A query response is a <em>top-level JSON array</em> of these: one
///         <see cref="Header" /> item, then one <see cref="Row" /> item per result row, then a
///         <see cref="Fault" /> item if the server failed part-way through. Exactly one member is
///         set on any item, and the context's <c>WhenWritingNull</c> means the other two cost no
///         bytes.
///     </para>
///     <para>
///         <b>Why a tagged array and not the obvious object with a <c>rows</c> member.</b>
///         <c>JsonSerializer.DeserializeAsyncEnumerable</c> reads a <em>top-level</em> array
///         incrementally and nothing else, so a rows array nested inside an object would need a
///         hand-written <c>Utf8JsonReader</c> loop over a buffered stream on the client. The tag
///         costs about ten bytes a row and buys a source-generated reader on one side and a
///         source-generated writer on the other.
///     </para>
///     <para>
///         <b>Why the fault travels last rather than in the envelope.</b> Streaming means the
///         response status and the first rows are committed before the server knows whether the
///         query will finish — an EF translation failure raised on the first <c>MoveNext</c>, or a
///         store error a thousand rows in, cannot retroactively become an HTTP 500. So a failure is
///         a trailing item, and the client raises it. That is safe today <em>because</em>
///         <see cref="ClientResultMaterializer" /> decodes to completion (D7's buffering point 4,
///         which is deliberate): the trailing fault is always reached before any row reaches the
///         caller. A future half (B) that yields rows lazily has to answer this again.
///     </para>
/// </remarks>
public sealed record QueryStreamItem
{
    /// <summary>
    ///     The result metadata. Set on the first item of a successful response, and on no other.
    /// </summary>
    public QueryResultHeader? Header { get; init; }

    /// <summary>
    ///     One result row.
    /// </summary>
    public DynamicValueNode? Row { get; init; }

    /// <summary>
    ///     The server-side failure this response reports (wire-protocol W5). Always the last item
    ///     when present; a response carrying one has no more rows after it.
    /// </summary>
    public InfoCarrierFault? Fault { get; init; }
}

/// <summary>
///     What a streamed query response says about itself before its first row.
/// </summary>
/// <remarks>
///     Everything here is derived from the <em>query</em> rather than from the rows, because it has
///     to be written before a row has been seen. That is a change from the buffered format, where
///     the element type was the first non-null row's runtime type — see
///     <see cref="ElementTypeName" />.
/// </remarks>
public sealed record QueryResultHeader
{
    /// <summary>
    ///     The wire contract version the response speaks, echoing the request's.
    /// </summary>
    public required int ProtocolVersion { get; init; }

    /// <summary>
    ///     Whether the result element type is an entity type the server's model knows.
    /// </summary>
    public required bool IsEntityResult { get; init; }

    /// <summary>
    ///     The element type name, for diagnostics and projection routing.
    /// </summary>
    /// <remarks>
    ///     The type the <em>query</em> declares, not the first row's runtime type. The two differ
    ///     only where a row is a proxy or a derived type, and the declared one is the better answer
    ///     of the two: a lazy-loading proxy's CLR type is not in the model, so the buffered format
    ///     reported <c>IsEntityResult: false</c> for a result that was entirely entities.
    /// </remarks>
    public string? ElementTypeName { get; init; }
}
