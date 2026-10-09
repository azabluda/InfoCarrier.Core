// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using InfoCarrier.Core.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
///         the caller's own signal and still propagates. Diagnostics use the request token
///         to distinguish expected cancellation from an unrelated cancellation exception.
///     </para>
///     <para>
///         <b>Deliberately free of sample types</b>, so promoting it into an
///         <c>InfoCarrier.Core.AspNetCore</c> package is a file move (spec 4.1).
///     </para>
/// </remarks>
public static class InfoCarrierEndpointExtensions
{
    /// <summary>
    ///     Maps the endpoint a client's transport posts its envelopes to.
    /// </summary>
    /// <param name="endpoints">The route builder to map onto.</param>
    /// <param name="pattern">
    ///     The route pattern. Must agree with the client's request URI, which defaults to the
    ///     same value.
    /// </param>
    /// <returns>The mapped endpoint, so the caller can add its own conventions.</returns>
    public static IEndpointConventionBuilder MapInfoCarrier(
        this IEndpointRouteBuilder endpoints,
        string pattern = "infocarrier")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Fail trusted catalog construction during endpoint setup, before requests arrive.
        _ = endpoints.ServiceProvider.GetServices<Expressions.AnonymousShapeCatalog>().ToArray();

        return endpoints.MapPost(pattern, async (HttpContext http) =>
        {
            ILogger? logger = EndpointLogger(http.RequestServices);
            string phase = "request body reading";
            bool dispatching = false;
            try
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
                    Log(logger, LogLevel.Warning, 35010, "InfoCarrier request was rejected because its envelope could not be read.");
                    phase = "bad request response writing";
                    await WriteBadRequestAsync(http, exception.Message).ConfigureAwait(false);
                    return;
                }

                var server = new InfoCarrierEnvelopeServer(
                    http.RequestServices.GetRequiredService<IInfoCarrierServer>(), serializer,
                    DispatcherLogger(http.RequestServices));
                InfoCarrierEnvelope response;
                try
                {
                    dispatching = true;
                    response = await server.DispatchAsync(request, http.RequestAborted).ConfigureAwait(false);
                }
                catch (NotSupportedException exception)
                {
                    dispatching = false;
                    phase = "bad request response writing";
                    await WriteBadRequestAsync(http, exception.Message).ConfigureAwait(false);
                    return;
                }

                dispatching = false;
                http.Response.ContentType = "application/json";
                phase = "response envelope serialization";
                byte[] bytes = serializer.Serialize(response);
                phase = "response body writing";
                await http.Response.Body.WriteAsync(bytes, http.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Dispatch owns its failures. The adapter observes only HTTP boundary failures.
                if (!dispatching)
                {
                    bool cancelled = http.RequestAborted.IsCancellationRequested
                        && exception is OperationCanceledException or IOException;
                    Log(logger, cancelled ? LogLevel.Debug : LogLevel.Error, 35011,
                        cancelled ? "InfoCarrier HTTP request was cancelled during {Phase}."
                            : "InfoCarrier HTTP request failed during {Phase}.", phase);
                }
                throw;
            }
        });
    }

    private static ILogger? EndpointLogger(IServiceProvider services)
    {
        try { return services.GetService<ILoggerFactory>()?.CreateLogger("InfoCarrier.Core.AspNetCore.InfoCarrierEndpoint"); }
        catch (Exception) { return null; }
    }

    private static ILogger<InfoCarrierEnvelopeServer> DispatcherLogger(IServiceProvider services)
    {
        try
        {
            return services.GetService<ILogger<InfoCarrierEnvelopeServer>>()
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InfoCarrierEnvelopeServer>.Instance;
        }
        catch (Exception) { return Microsoft.Extensions.Logging.Abstractions.NullLogger<InfoCarrierEnvelopeServer>.Instance; }
    }

    private static void Log(ILogger? logger, LogLevel level, int eventId, string message, params object?[] values)
    {
        try { logger?.Log(level, new EventId(eventId), message, values); }
        catch (Exception) { }
    }

    private static async Task WriteBadRequestAsync(HttpContext http, string message)
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        http.Response.ContentType = "text/plain; charset=utf-8";
        await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(message), http.RequestAborted)
            .ConfigureAwait(false);
    }
}
