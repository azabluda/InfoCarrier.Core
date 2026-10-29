// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core;

/// <summary>
///     A permanent disagreement about the wire contract. Retrying the same request cannot fix it.
/// </summary>
/// <param name="clientVersion">The client's wire version, or zero when identification is missing.</param>
/// <param name="serverVersion">The server's wire version.</param>
public sealed class InfoCarrierProtocolException(int clientVersion, int serverVersion)
    : NotSupportedException(
        $"InfoCarrier protocol version mismatch: client {clientVersion}, server {serverVersion}. "
        + (clientVersion < serverVersion ? "Upgrade the client." : "Upgrade the server."))
{
    /// <summary>Gets the client's wire version, or zero when identification is missing.</summary>
    public int ClientVersion { get; } = clientVersion;

    /// <summary>Gets the server's wire version.</summary>
    public int ServerVersion { get; } = serverVersion;
}
