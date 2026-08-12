// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     Wraps the inner handler of an <see cref="HttpClient" /> and records each request/response
///     pair that passes through it: how many requests were sent, and the raw bytes of each
///     response body, in send order.
/// </summary>
/// <remarks>
///     Deliberately reusable rather than throwaway: a later phase needs a wire-inspector panel
///     that shows exactly this (request count, payload contents) for a running client, and this
///     is a small prototype of that seam. Kept thread-safe enough for the sequential use a test
///     makes of it -- one client, one request at a time -- not for concurrent request pipelining,
///     which this provider does not do.
///
///     No custom constructor: left with the default parameterless one that <see cref="DelegatingHandler" />
///     already provides, and <c>InnerHandler</c> unset, because <c>WebApplicationFactory.CreateDefaultClient</c>
///     chains handlers by assigning <c>InnerHandler</c> itself -- a handler that arrives with one
///     already set would just have it overwritten.
/// </remarks>
public sealed class RecordingHandler : DelegatingHandler
{
    private readonly List<byte[]> responseBodies = [];
    private int requestCount;

    /// <summary>The number of requests sent through this handler so far.</summary>
    public int RequestCount => this.requestCount;

    /// <summary>The response body bytes for each request that has completed, in send order.</summary>
    public IReadOnlyList<byte[]> ResponseBodies
    {
        get
        {
            lock (this.responseBodies)
            {
                return this.responseBodies.ToArray();
            }
        }
    }

    /// <summary>Clears the recorded count and bodies, for tests that reuse one handler across phases.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref this.requestCount, 0);
        lock (this.responseBodies)
        {
            this.responseBodies.Clear();
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref this.requestCount);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // ReadAsByteArrayAsync buffers the content; the transport's own subsequent read of the
        // same HttpContent (to deserialize the envelope) still works, because HttpContent only
        // ever reads its underlying stream once and serves buffered reads after that.
        byte[] body = response.Content is null
            ? []
            : await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        lock (this.responseBodies)
        {
            this.responseBodies.Add(body);
        }

        return response;
    }
}
