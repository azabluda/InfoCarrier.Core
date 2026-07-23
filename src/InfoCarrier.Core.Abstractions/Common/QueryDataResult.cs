// Licensed under the MIT license. See license.txt file in the project root for license information.

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
    /// <summary>
    ///     The serialized result rows. The shape is defined by the result-mapping
    ///     contract (identity-keyed entity rows, or columnar projection rows).
    /// </summary>
    public required byte[] SerializedResults { get; init; }

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
