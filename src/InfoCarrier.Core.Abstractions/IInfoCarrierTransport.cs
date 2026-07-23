// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

/// <summary>
///     Transport seam between client and server (wire-protocol §6). Concrete bindings
///     (ASP.NET Core endpoint, gRPC service, in-process test transport) implement this.
///     The in-process JSON round-trip transport (v1's <c>SimulateNetworkTransferJson</c>
///     pattern) is the first binding, used by the functional-test harness.
/// </summary>
public interface IInfoCarrierTransport
{
    /// <summary>
    ///     Sends a request envelope and returns the response envelope.
    /// </summary>
    /// <param name="request">The request envelope.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The response envelope.</returns>
    Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default);
}
