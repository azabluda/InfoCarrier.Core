// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;

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
/// <param name="queryHandler">
///     The in-process handler for a streamed query response, normally
///     <see cref="InfoCarrierEnvelopeServer.DispatchQueryAsync" />. Null for a transport that is
///     only ever asked to carry the other eight operations, which is what the token test needs.
/// </param>
public sealed class InProcessInfoCarrierTransport(
    Func<InfoCarrierEnvelope, CancellationToken, Task<InfoCarrierEnvelope>> handler,
    IInfoCarrierSerializer serializer,
    Func<InfoCarrierEnvelope, CancellationToken, IAsyncEnumerable<QueryStreamItem>>? queryHandler = null)
    : IInfoCarrierTransport
{
    private readonly Func<InfoCarrierEnvelope, CancellationToken, Task<InfoCarrierEnvelope>> _handler = handler;
    private readonly IInfoCarrierSerializer _serializer = serializer;

    private readonly Func<InfoCarrierEnvelope, CancellationToken, IAsyncEnumerable<QueryStreamItem>>? _queryHandler
        = queryHandler;

    /// <inheritdoc />
    public async Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
    {
        // Round-trip the request through real serialization, then dispatch.
        InfoCarrierEnvelope simulatedRequest = await SimulateAsync(request, cancellationToken).ConfigureAwait(false);
        InfoCarrierEnvelope response = await _handler(simulatedRequest, cancellationToken).ConfigureAwait(false);

        // Round-trip the response before handing it back.
        return await SimulateAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The same simulation as <see cref="SendAsync" />, one item at a time: the request
    ///     envelope is round-tripped whole, and every <see cref="QueryStreamItem" /> the server
    ///     produces is serialized and deserialized before the client sees it. <b>Per item rather
    ///     than per response, because that is what the wire now does</b> — and because a simulation
    ///     that buffered the items in order to round-trip them together would be testing the
    ///     opposite of the thing under test.
    /// </remarks>
    public Task<QueryDataResult> SendQueryAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
    {
        Func<InfoCarrierEnvelope, CancellationToken, IAsyncEnumerable<QueryStreamItem>> queries =
            _queryHandler
            ?? throw new InvalidOperationException(
                $"This {nameof(InProcessInfoCarrierTransport)} was built without a query handler, "
                + "so it cannot carry a Query operation.");

        return QueryStreamReader.ReadAsync(
            SimulateQueryAsync(request, queries, cancellationToken),
            "the in-process transport",
            owner: null,
            cancellationToken);
    }

    private async IAsyncEnumerable<QueryStreamItem> SimulateQueryAsync(
        InfoCarrierEnvelope request,
        Func<InfoCarrierEnvelope, CancellationToken, IAsyncEnumerable<QueryStreamItem>> queries,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        InfoCarrierEnvelope simulatedRequest = await SimulateAsync(request, cancellationToken).ConfigureAwait(false);

        await foreach (QueryStreamItem item in
            queries(simulatedRequest, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return RoundTrip(item);
        }
    }

    /// <summary>
    ///     One response item through real serialization.
    /// </summary>
    /// <remarks>
    ///     Through <see cref="ExpressionJsonContext" /> rather than through the injected
    ///     <see cref="IInfoCarrierSerializer" />, because that is the context the real bindings use
    ///     for a query response — a <see cref="Expressions.DynamicValueNode" /> is only correct
    ///     under its options. Simulating the wire with a different one would prove the wrong thing.
    /// </remarks>
    private static QueryStreamItem RoundTrip(QueryStreamItem item)
        => System.Text.Json.JsonSerializer.Deserialize(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                item, ExpressionJsonContext.Default.QueryStreamItem),
            ExpressionJsonContext.Default.QueryStreamItem)!;

    private async Task<InfoCarrierEnvelope> SimulateAsync(InfoCarrierEnvelope value, CancellationToken cancellationToken)
    {
        byte[] payload = await _serializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);
        return (await _serializer.DeserializeAsync<InfoCarrierEnvelope>(payload, cancellationToken).ConfigureAwait(false))!;
    }
}
