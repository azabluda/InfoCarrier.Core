// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common;

/// <summary>
///     The result of beginning a server transaction (wire-protocol §2.3, W3).
///     The <see cref="TransactionId" /> is the token representing the open server
///     transaction across (possibly stateless) transports.
/// </summary>
public sealed record TransactionResult
{
    /// <summary>
    ///     A token identifying the open server-side transaction.
    /// </summary>
    public required string TransactionId { get; init; }
}
