// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     In-process <see cref="IInfoCarrierTransport" /> that round-trips every envelope
///     through real serialization (v1's <c>SimulateNetworkTransferJson</c> pattern,
///     modernized to the <see cref="IInfoCarrierSerializer" /> seam). Wire-serializability
///     failures surface in tests exactly as they would over a network.
/// </summary>
/// <remarks>
///     <b>Moved out of the product 2026-08-17 (M8-21), because it is a test harness rather than a
///     transport.</b> It double-serializes on purpose — request and response both — which is right
///     for proving serializability and wrong for any deployment. It was referenced by three test
///     files and by nothing in <c>src/</c> or <c>samples/</c> for its whole life. A real in-process
///     deployment needs no transport at all: <c>InfoCarrierEnvelopeServer.DispatchAsync</c> is
///     already a delegate the caller can hand to <see cref="IInfoCarrierTransport" /> directly.
/// </remarks>
/// <remarks>
///     The handler receives the <em>deserialized</em> request envelope (a fresh instance,
///     proving serializability) and returns a response envelope, which is itself
///     round-tripped before being handed back to the caller.
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="InProcessInfoCarrierTransport" /> class.
/// </remarks>
/// <param name="handler">The in-process handler standing in for the network server.</param>
/// <param name="serializer">The serializer used to simulate the wire.</param>
public sealed class InProcessInfoCarrierTransport(
    Func<InfoCarrierEnvelope, CancellationToken, Task<InfoCarrierEnvelope>> handler,
    IInfoCarrierSerializer serializer) : IInfoCarrierTransport
{
    private readonly Func<InfoCarrierEnvelope, CancellationToken, Task<InfoCarrierEnvelope>> _handler = handler;
    private readonly IInfoCarrierSerializer _serializer = serializer;

    /// <inheritdoc />
    public async Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
    {
        // Round-trip the request through real serialization, then dispatch.
        InfoCarrierEnvelope simulatedRequest = await SimulateAsync(request, cancellationToken).ConfigureAwait(false);
        InfoCarrierEnvelope response = await _handler(simulatedRequest, cancellationToken).ConfigureAwait(false);

        // Round-trip the response before handing it back.
        return await SimulateAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InfoCarrierEnvelope> SimulateAsync(InfoCarrierEnvelope value, CancellationToken cancellationToken)
    {
        byte[] payload = await _serializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);
        return (await _serializer.DeserializeAsync<InfoCarrierEnvelope>(payload, cancellationToken).ConfigureAwait(false))!;
    }
}
