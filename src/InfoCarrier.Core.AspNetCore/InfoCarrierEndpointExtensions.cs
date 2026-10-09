// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using InfoCarrier.Core.Common;
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

        InfoCarrierServerDiagnostics? diagnostics = endpoints.ServiceProvider.GetService<InfoCarrierServerDiagnostics>();

        return endpoints.MapPost(pattern, async (HttpContext http) =>
        {
            using IDisposable? diagnosticScope = diagnostics?.BeginRequest(null,
                LoggerFactory(http.RequestServices), Caller(http.RequestServices));
            try
            {
                IInfoCarrierSerializer serializer = http.RequestServices.GetRequiredService<IInfoCarrierSerializer>();

                using var buffer = new MemoryStream();
                await http.Request.Body.CopyToAsync(buffer, http.RequestAborted).ConfigureAwait(false);

                InfoCarrierEnvelope request;
                try
                {
                    request = serializer.Deserialize<InfoCarrierEnvelope>(buffer.ToArray())!;
                    if (request is null)
                    {
                        var failure = new InvalidOperationException("The request body is not an InfoCarrier envelope.");
                        diagnostics?.ReportFailure(failure, http.RequestAborted, InfoCarrierServerFailureReason.InvalidPayload);
                        throw failure;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    diagnostics?.ReportFailure(exception, http.RequestAborted);
                    await WriteBadRequestAsync(http, exception.Message).ConfigureAwait(false);
                    return;
                }

                var envelopeServer = new InfoCarrierEnvelopeServer(
                    http.RequestServices.GetRequiredService<IInfoCarrierServer>(), serializer, diagnostics);

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
                diagnostics?.SetPhase(InfoCarrierServerPhase.EnvelopeSerialization);
                byte[] bytes = serializer.Serialize(response);
                diagnostics?.SetPhase(InfoCarrierServerPhase.ResponseWrite);
                await http.Response.Body.WriteAsync(bytes, http.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                diagnostics?.ReportFailure(exception, http.RequestAborted);
                throw;
            }
        });
    }

    private static Microsoft.Extensions.Logging.ILoggerFactory? LoggerFactory(IServiceProvider services)
    {
        try { return services.GetService<Microsoft.Extensions.Logging.ILoggerFactory>(); }
        catch (Exception) { return null; }
    }

    private static string? Caller(IServiceProvider services)
    {
        try { return services.GetService<IInfoCarrierServerCallerIdentity>()?.CurrentCallerId; }
        catch (Exception) { return null; }
    }

    private static async Task WriteBadRequestAsync(HttpContext http, string message)
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        http.Response.ContentType = "text/plain; charset=utf-8";
        await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(message), http.RequestAborted)
            .ConfigureAwait(false);
    }
}
