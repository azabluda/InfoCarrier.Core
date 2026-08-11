using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Northwind.Server.Transport;

/// <summary>
///     Maps the InfoCarrier envelope endpoint.
/// </summary>
/// <remarks>
///     <para>
///         All nine operations, one route. The product's <see cref="InfoCarrierEnvelopeServer" />
///         already checks the protocol version, dispatches, and turns a server-side failure into
///         a fault carried in the response (W5), so this adds no policy of its own.
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

            InfoCarrierEnvelope request =
                serializer.Deserialize<InfoCarrierEnvelope>(buffer.ToArray())
                ?? throw new InvalidOperationException("The request body is not an InfoCarrier envelope.");

            var envelopeServer = new InfoCarrierEnvelopeServer(
                http.RequestServices.GetRequiredService<IInfoCarrierServer>(), serializer);

            InfoCarrierEnvelope response =
                await envelopeServer.DispatchAsync(request, http.RequestAborted).ConfigureAwait(false);

            http.Response.ContentType = "application/json";
            await http.Response.Body.WriteAsync(serializer.Serialize(response), http.RequestAborted)
                .ConfigureAwait(false);
        });
    }
}
