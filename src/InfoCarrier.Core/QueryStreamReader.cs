// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core;

/// <summary>
///     Turns a streamed query response — a sequence of <see cref="QueryStreamItem" /> — into the
///     <see cref="QueryDataResult" /> the client works with.
/// </summary>
/// <remarks>
///     <para>
///         Shared by every <see cref="IInfoCarrierTransport" /> that carries a query, so that all
///         of them read the same protocol: the header is awaited eagerly (it is what
///         <see cref="QueryDataResult" />'s own members are), and the rows stay lazy behind it.
///         A transport that wrote this itself would be free to get the fault handling subtly
///         different, which is the one thing a wire must not leave to an implementer.
///     </para>
///     <para>
///         <b>A fault is raised, never returned.</b> It reaches the caller as the exception the
///         server actually threw, rebuilt by <see cref="InfoCarrierFaultMapper" /> — the same
///         mapper and the same fidelity as the buffered path, which is what keeps the thousands of
///         spec assertions on exception type and message meaningful over a wire.
///     </para>
/// </remarks>
public static class QueryStreamReader
{
    /// <summary>
    ///     Reads the header off <paramref name="items" /> and returns a result whose rows are the
    ///     rest of it.
    /// </summary>
    /// <param name="items">The response items, in wire order.</param>
    /// <param name="origin">
    ///     What answered — a request URI, or a description of an in-process server. Named in the
    ///     message when the response is not a query response at all, because "the server sent
    ///     something else" is useless without saying which server.
    /// </param>
    /// <param name="owner">
    ///     A resource the response is read from, disposed once the rows are exhausted or the
    ///     enumerator is disposed. The HTTP binding passes its <c>HttpResponseMessage</c>.
    /// </param>
    /// <param name="cancellationToken">A token to observe while reading.</param>
    public static async Task<QueryDataResult> ReadAsync(
        IAsyncEnumerable<QueryStreamItem> items,
        string origin,
        IDisposable? owner = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        IAsyncEnumerator<QueryStreamItem> enumerator = items.GetAsyncEnumerator(cancellationToken);

        try
        {
            if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                throw new InfoCarrierTransportException(
                    $"The InfoCarrier server at '{origin}' answered a query with an empty response. "
                    + "A query response carries a header item before anything else.");
            }

            QueryStreamItem first = enumerator.Current;

            // The fault first, and before the header is looked at (wire-protocol W5). A server
            // that failed before it could describe the result sends nothing but the fault.
            if (first.Fault is { } fault)
            {
                throw InfoCarrierFaultMapper.Rehydrate(fault);
            }

            if (first.Header is not { } header)
            {
                throw new InfoCarrierTransportException(
                    $"The InfoCarrier server at '{origin}' answered a query with an item that is "
                    + "neither a header nor a fault. The first item of a query response is always one of the two.");
            }

            return new QueryDataResult
            {
                IsEntityResult = header.IsEntityResult,
                ElementTypeName = header.ElementTypeName,
                Rows = Rows(enumerator, owner),
            };
        }
        catch
        {
            // Only on the way out through an exception: on the success path the enumerator and the
            // response belong to the sequence below, which disposes both when it finishes.
            await enumerator.DisposeAsync().ConfigureAwait(false);
            owner?.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     The rows behind the header, and the owner of everything the response is being read from.
    /// </summary>
    /// <remarks>
    ///     The <c>finally</c> is what makes an abandoned enumeration merely wasteful rather than a
    ///     leak: <c>await foreach</c> disposes the enumerator when the caller stops early, which
    ///     runs this and closes the response.
    /// </remarks>
    private static async IAsyncEnumerable<DynamicValueNode> Rows(
        IAsyncEnumerator<QueryStreamItem> enumerator,
        IDisposable? owner)
    {
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                QueryStreamItem item = enumerator.Current;

                if (item.Fault is { } fault)
                {
                    throw InfoCarrierFaultMapper.Rehydrate(fault);
                }

                if (item.Row is { } row)
                {
                    yield return row;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
            owner?.Dispose();
        }
    }
}
