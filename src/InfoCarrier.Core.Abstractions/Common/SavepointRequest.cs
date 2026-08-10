// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common;

/// <summary>
///     Names a savepoint inside an open server transaction (wire-protocol §2.3, W3).
/// </summary>
/// <remarks>
///     A savepoint is not a scope of its own, so it has no token: the transaction's token plus a
///     name is the whole address.
/// </remarks>
public sealed record SavepointRequest : IInfoCarrierRequest
{
    /// <summary>
    ///     The open server transaction the savepoint lives in.
    /// </summary>
    public required string TransactionId { get; init; }

    /// <summary>
    ///     The savepoint's name, as the caller gave it to EF.
    /// </summary>
    public required string Name { get; init; }
}
