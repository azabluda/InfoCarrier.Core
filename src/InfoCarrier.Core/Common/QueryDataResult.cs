// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core.Common;

/// <summary>
///     A query result: materialized rows plus metadata needed for client-side
///     materialization, identity resolution, and projection (wire-protocol §2.1).
/// </summary>
/// <remarks>
///     For entity types, rows are identity-keyed with loaded-navigation markers so the
///     client can resolve identity and fix up navigations (requirements §2.5). For
///     non-entity projections, the server returns type-agnostic columnar data and the
///     client applies the final projection locally (requirements §3.2).
/// </remarks>
public sealed record QueryDataResult
{
    private IReadOnlyList<DynamicValueNode>? _rows;
    private byte[]? _serializedResults;

    /// <summary>The typed result graph carried by protocol 4.</summary>
    [JsonRequired]
    public IReadOnlyList<DynamicValueNode> Rows
    {
        get => _rows ?? (_serializedResults is null ? []
            : JsonSerializer.Deserialize(_serializedResults, ExpressionJsonContext.Default.ListDynamicValueNode)!);
        init { _rows = value; _serializedResults = null; }
    }
    /// <summary>
    ///     An in-process source compatibility accessor for adapters using encoded rows.
    ///     Protocol 4 serializes <see cref="Rows" /> instead; this accessor is excluded from wire metadata.
    /// </summary>
    [JsonIgnore]
    public byte[] SerializedResults
    {
        get => _serializedResults ?? JsonSerializer.SerializeToUtf8Bytes(_rows?.ToList() ?? [], ExpressionJsonContext.Default.ListDynamicValueNode);
        init { _serializedResults = value; _rows = null; }
    }

    /// <summary>
    ///     Whether the result element type is an entity type the server's model knows.
    ///     When false, the client applies the final projection locally.
    /// </summary>
    public required bool IsEntityResult { get; init; }

    /// <summary>
    ///     The element type name, for diagnostics and projection routing.
    /// </summary>
    public string? ElementTypeName { get; init; }

    /// <summary>
    ///     What the server logged while running this query, or <see langword="null" /> when the
    ///     server does not forward its log (the default) or logged nothing.
    /// </summary>
    /// <remarks>
    ///     See <see cref="InfoCarrierServerOptions.LogForwarding" /> for the grant and what it discloses.
    /// </remarks>
    public IReadOnlyList<ServerLogEvent>? ServerLog { get; init; }
}
