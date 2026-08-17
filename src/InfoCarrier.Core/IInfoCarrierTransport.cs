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

    /// <summary>
    ///     Sends an <see cref="InfoCarrierOperation.Query" /> envelope and returns its result as
    ///     the rows arrive (<c>docs/architecture.md</c> §6a <b>D7</b>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Why a query needs its own method.</b> Every other operation answers with one
    ///         value, which <see cref="SendAsync" /> can return once it has all of it. A query
    ///         answers with a sequence that is worth having before it is complete, and a
    ///         <see cref="Task{TResult}" /> of a fully-formed envelope is precisely the shape that
    ///         cannot express that. The response is a <see cref="QueryStreamItem" /> array;
    ///         <see cref="QueryStreamReader" /> is the shared reader for it, and a binding is
    ///         strongly advised to use it rather than parse the protocol again.
    ///     </para>
    ///     <para>
    ///         The returned <see cref="QueryDataResult.Rows" /> is live and holds server-side
    ///         resources open — see <see cref="QueryDataResult" /> for who owns what.
    ///     </para>
    /// </remarks>
    /// <param name="request">The request envelope, whose operation is
    ///     <see cref="InfoCarrierOperation.Query" />.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The query result, with its rows still arriving.</returns>
    Task<QueryDataResult> SendQueryAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default);
}
