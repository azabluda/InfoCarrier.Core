// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using System.Text.Json;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.AspNetCore;

/// <summary>
///     Maps the InfoCarrier envelope endpoint.
/// </summary>
/// <remarks>
///     <para>
///         All nine operations, one route. The product's <see cref="InfoCarrierEnvelopeServer" />
///         already checks the protocol version, dispatches, and turns a server-side failure into
///         a fault carried in the response (W5), so this adds no policy of its own — with two
///         deliberate exceptions, both outside that response-as-data path by design and both
///         answered here as a plain HTTP 400 whose body is only the exception's own message: no
///         stack trace, no server file paths.
///     </para>
///     <para>
///         <b>The first</b> is a request body that does not deserialize into an
///         <see cref="InfoCarrierEnvelope" /> — the failure happens before there is an envelope to
///         hand to <see cref="InfoCarrierEnvelopeServer.DispatchAsync" /> at all.
///     </para>
///     <para>
///         <b>The second</b> is <see cref="NotSupportedException" /> from a protocol-version
///         mismatch. <see cref="InfoCarrierEnvelopeServer.DispatchAsync" /> checks the version
///         before its own try/catch on purpose (see that method's remarks): the two ends disagree
///         about what an envelope even is, so answering with one would be optimistic. Left
///         uncaught, ASP.NET Core's default 500 handling would leak that decision as a raw
///         exception (a bare stack trace under the Developer Exception Page, or a generic
///         <c>text/plain</c> 500 with no message at all outside Development) instead of naming
///         both protocol versions, which is the whole reason
///         <see cref="InfoCarrierEnvelopeServer" /> writes that message in the first place.
///     </para>
///     <para>
///         Neither catch touches <see cref="OperationCanceledException" />: a cancelled request is
///         the caller's own signal, not a server-side failure to report.
///     </para>
///     <para>
///         <b>Deliberately free of sample types</b>, so promoting it into an
///         <c>InfoCarrier.Core.AspNetCore</c> package is a file move (spec 4.1).
///     </para>
/// </remarks>
public static class InfoCarrierEndpointExtensions
{
    public static IEndpointConventionBuilder MapInfoCarrier(
        this IEndpointRouteBuilder endpoints,
        string pattern = "infocarrier")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapPost(pattern, async (HttpContext http) =>
        {
            IInfoCarrierSerializer serializer = http.RequestServices.GetRequiredService<IInfoCarrierSerializer>();

            using var buffer = new MemoryStream();
            await http.Request.Body.CopyToAsync(buffer, http.RequestAborted).ConfigureAwait(false);

            InfoCarrierEnvelope request;
            try
            {
                request = serializer.Deserialize<InfoCarrierEnvelope>(buffer.ToArray())
                    ?? throw new InvalidOperationException("The request body is not an InfoCarrier envelope.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await WriteBadRequestAsync(http, exception.Message).ConfigureAwait(false);
                return;
            }

            var envelopeServer = new InfoCarrierEnvelopeServer(
                http.RequestServices.GetRequiredService<IInfoCarrierServer>(), serializer);

            if (request.Operation == InfoCarrierOperation.Query)
            {
                await StreamQueryAsync(http, envelopeServer, request).ConfigureAwait(false);
                return;
            }

            InfoCarrierEnvelope response;
            try
            {
                response = await envelopeServer.DispatchAsync(request, http.RequestAborted).ConfigureAwait(false);
            }
            catch (NotSupportedException exception)
            {
                await WriteBadRequestAsync(http, exception.Message).ConfigureAwait(false);
                return;
            }

            http.Response.ContentType = "application/json";
            await http.Response.Body.WriteAsync(serializer.Serialize(response), http.RequestAborted)
                .ConfigureAwait(false);
        });
    }

    private static readonly byte[] ArrayStart = "["u8.ToArray();
    private static readonly byte[] ArrayEnd = "]"u8.ToArray();
    private static readonly byte[] ItemSeparator = ","u8.ToArray();

    /// <summary>
    ///     Writes a query response as a <see cref="QueryStreamItem" /> array, as the rows are
    ///     produced (<c>docs/architecture.md</c> §6a <b>D7</b>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The first item is pulled before anything is written, and that is what keeps the
    ///         400 available.</b> A protocol-version mismatch is raised by
    ///         <see cref="InfoCarrierEnvelopeServer.DispatchQueryAsync" /> on the first
    ///         <c>MoveNext</c>, and once a byte of a 200 has gone out there is no way to answer
    ///         with a status code any more. Everything else is already a fault item by the time it
    ///         gets here, which is the point of the trailing-fault design.
    ///     </para>
    ///     <para>
    ///         <b>The header is flushed on its own</b>, before the first row exists. That is what
    ///         time-to-first-byte means here: the client can start decoding — and, once half (B)
    ///         lands, hand rows out — while the server is still reading the store. After that the
    ///         response body's own pipe decides when to push, which is what it is for; flushing
    ///         per row would put a chunk header on every row of a million-row result to buy
    ///         nothing.
    ///     </para>
    ///     <para>
    ///         <b>The array is framed by hand, and that is forced rather than perverse.</b> The
    ///         obvious shape — one <c>Utf8JsonWriter</c> over the response body, with
    ///         <c>writer.WriteStartArray()</c> and a <c>JsonSerializer.Serialize(writer, …)</c> per
    ///         item — <b>cannot work in ASP.NET Core</b>: that overload calls <c>Flush()</c> on the
    ///         writer when it finishes a value, and a synchronous write to the response body throws
    ///         <c>"Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO
    ///         to true"</c>. It was written that way first and 8 of the 17 transport tests said so.
    ///         <c>SerializeAsync</c> has no such overload for a writer, so the three punctuation
    ///         bytes are written directly and each item is serialized to the stream.
    ///     </para>
    /// </remarks>
    private static async Task StreamQueryAsync(
        HttpContext http,
        InfoCarrierEnvelopeServer envelopeServer,
        InfoCarrierEnvelope request)
    {
        CancellationToken cancellationToken = http.RequestAborted;

        await using IAsyncEnumerator<QueryStreamItem> items = envelopeServer
            .DispatchQueryAsync(request, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        bool any;
        try
        {
            any = await items.MoveNextAsync().ConfigureAwait(false);
        }
        catch (NotSupportedException exception)
        {
            await WriteBadRequestAsync(http, exception.Message).ConfigureAwait(false);
            return;
        }

        http.Response.ContentType = "application/json";

        Stream body = http.Response.Body;
        await body.WriteAsync(ArrayStart, cancellationToken).ConfigureAwait(false);

        bool first = true;
        while (any)
        {
            if (!first)
            {
                await body.WriteAsync(ItemSeparator, cancellationToken).ConfigureAwait(false);
            }

            await JsonSerializer.SerializeAsync(
                body, items.Current, ExpressionJsonContext.Default.QueryStreamItem, cancellationToken)
                .ConfigureAwait(false);

            if (first)
            {
                // The header, on its own, before the query has produced a row.
                await body.FlushAsync(cancellationToken).ConfigureAwait(false);
                first = false;
            }

            any = await items.MoveNextAsync().ConfigureAwait(false);
        }

        await body.WriteAsync(ArrayEnd, cancellationToken).ConfigureAwait(false);
        await body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteBadRequestAsync(HttpContext http, string message)
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        http.Response.ContentType = "text/plain; charset=utf-8";
        await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(message), http.RequestAborted)
            .ConfigureAwait(false);
    }
}
