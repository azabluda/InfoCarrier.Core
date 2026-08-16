// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace Northwind.Demo;

/// <summary>
///     Counts outbound requests, so the demo can show how many round trips a piece of LINQ
///     actually cost.
/// </summary>
/// <remarks>
///     <para>
///         Deliberately separate from the test project's <c>RecordingHandler</c>, which also
///         captures bodies. This one only counts, because that is all the demo prints — a sample
///         carrying machinery it does not use teaches the wrong thing.
///     </para>
///     <para>
///         It counted response bytes too, until the first end-to-end run printed
///         <c>0 bytes received</c>: the endpoint writes to the response stream without setting
///         <c>Content-Length</c>, so the header is null and the sum was always zero. Reading the
///         body here to measure it would consume the stream the transport is about to read, so the
///         counter was removed rather than made to look right.
///     </para>
/// </remarks>
internal sealed class CountingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests++;

        return base.SendAsync(request, cancellationToken);
    }
}
