// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

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

        InfoCarrierMetricIdentity? metricIdentity = null;
        string mediaType = "application/json";
        if (endpoints.ServiceProvider.GetService<InfoCarrierServerRegistrationStore>() is { } store)
        {
            metricIdentity = store.Default.MetricIdentity;
            mediaType = store.Default.MediaType;
        }
        return MapEndpoint(endpoints, pattern, provider =>
        {
            IInfoCarrierServer server = provider.GetRequiredService<IInfoCarrierServer>();
            return (server is DefaultInfoCarrierServer facade ? facade.Server : server,
                provider.GetRequiredService<IInfoCarrierSerializer>());
        }, metricIdentity, mediaType);
    }

    /// <summary>Maps a named server to the supplied route pattern.</summary>
    public static IEndpointConventionBuilder MapInfoCarrier(
        this IEndpointRouteBuilder endpoints, string pattern, string serverName)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        InfoCarrierServerRegistrationStore store = GetStore(endpoints);
        return MapSelected(endpoints, pattern, store, store.Get(serverName).Identity);
    }

    /// <summary>Maps the registration retained by a server builder from this host.</summary>
    public static IEndpointConventionBuilder MapInfoCarrier(
        this IEndpointRouteBuilder endpoints, InfoCarrierServerBuilder server, string pattern = "infocarrier")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(server);
        return MapSelected(endpoints, pattern, GetStore(endpoints), server.Identity);
    }

    /// <summary>Maps the only server registration for the context type; rejects missing or ambiguous matches.</summary>
    public static IEndpointConventionBuilder MapInfoCarrier<TContext>(
        this IEndpointRouteBuilder endpoints, string pattern = "infocarrier") where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        InfoCarrierServerRegistrationStore store = GetStore(endpoints);
        return MapSelected(endpoints, pattern, store, store.GetForContext(typeof(TContext)).Identity);
    }

    private static InfoCarrierServerRegistrationStore GetStore(IEndpointRouteBuilder endpoints)
        => endpoints.ServiceProvider.GetService<InfoCarrierServerRegistrationStore>()
            ?? throw new InvalidOperationException("No InfoCarrier servers are registered. Call AddInfoCarrierServer before mapping endpoints.");

    private static IEndpointConventionBuilder MapSelected(
        IEndpointRouteBuilder endpoints, string pattern, InfoCarrierServerRegistrationStore store,
        InfoCarrierRegistrationIdentity identity)
    {
        InfoCarrierServerActivation activation = store.Activate(endpoints.ServiceProvider, identity);
        return MapEndpoint(endpoints, pattern, _ => (activation.Server, activation.Serializer), store.Get(identity).MetricIdentity, store.Get(identity).MediaType);
    }

    private static IEndpointConventionBuilder MapEndpoint(
        IEndpointRouteBuilder endpoints, string pattern,
        Func<IServiceProvider, (IInfoCarrierServer Server, IInfoCarrierSerializer Serializer)> resolve,
        InfoCarrierMetricIdentity? metricIdentity, string mediaType)
    {
        // Validate the complete selected pair during endpoint setup, before requests arrive.
        _ = resolve(endpoints.ServiceProvider);

        // Fail trusted catalog construction during endpoint setup, before requests arrive.
        _ = endpoints.ServiceProvider.GetServices<Expressions.AnonymousShapeCatalog>().ToArray();

        return endpoints.MapPost(pattern, async (HttpContext http) =>
        {
            using InfoCarrierRequestMeasurement? measurement = InfoCarrierMetrics.StartEndpoint(metricIdentity);
            ILogger logger = http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("InfoCarrier.Core.AspNetCore.InfoCarrierEndpoint");
            string phase = "request body reading";
            bool dispatching = false;
            try
            {
                (IInfoCarrierServer selectedServer, IInfoCarrierSerializer serializer) = resolve(http.RequestServices);

                if (http.Request.Headers.TryGetValue("InfoCarrier-Operation", out var operationHeader)
                    && Enum.TryParse(operationHeader, out InfoCarrierOperation streamOperation)
                    && streamOperation == InfoCarrierOperation.Query)
                {
                    if (measurement is not null) { measurement.Operation = streamOperation; }
                    QueryDataRequest streamRequest = await serializer.DeserializeAsync<QueryDataRequest>(
                            http.Request.Body, http.RequestAborted).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The request body is not a query request.");
                    IInfoCarrierServer streamServer = selectedServer;
                    http.Response.ContentType = "application/json";
                    IAsyncEnumerable<QueryStreamFrame> frames = streamServer is IInfoCarrierStreamServer inProcess
                        ? inProcess.QueryFramesAsync(streamRequest, http.RequestAborted)
                        : BufferedFrames(streamServer, streamRequest, logger, http.RequestAborted);
                    await QueryStreamFrameCodec.WriteAsync(
                        http.Response.Body,
                        FaultFrames(frames, logger, measurement, http.RequestAborted),
                        http.RequestAborted).ConfigureAwait(false);
                    measurement?.Complete();
                    return;
                }

                InfoCarrierEnvelope request;
                try
                {
                    request = await serializer.DeserializeAsync<InfoCarrierEnvelope>(http.Request.Body, http.RequestAborted).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The request body is not an InfoCarrier envelope.");
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    measurement?.Fail(exception, http.RequestAborted);
                    Log(logger, LogLevel.Warning, 35010, "InfoCarrier request was rejected because its envelope could not be read.");
                    phase = "bad request response writing";
                    await WriteBadRequestAsync(http, exception.Message).ConfigureAwait(false);
                    return;
                }

                var server = new InfoCarrierEnvelopeServer(
                    selectedServer, serializer,
                    http.RequestServices.GetRequiredService<ILogger<InfoCarrierEnvelopeServer>>());
                InfoCarrierEnvelope response;
                if (measurement is not null) { measurement.Operation = request.Operation; }
                try
                {
                    dispatching = true;
                    response = await server.DispatchAsync(request, http.RequestAborted).ConfigureAwait(false);
                }
                catch (NotSupportedException exception)
                {
                    measurement?.Fail(exception, http.RequestAborted);
                    dispatching = false;
                    phase = "bad request response writing";
                    await WriteBadRequestAsync(http, exception.Message).ConfigureAwait(false);
                    return;
                }

                dispatching = false;
                if (response.Fault is { } responseFault)
                {
                    measurement?.FailFault(responseFault);
                }
                http.Response.ContentType = mediaType;
                phase = "response envelope serialization";
                byte[] bytes = serializer.Serialize(response);
                phase = "response body writing";
                await http.Response.Body.WriteAsync(bytes, http.RequestAborted).ConfigureAwait(false);
                measurement?.Complete();
            }
            catch (Exception exception)
            {
                measurement?.Fail(exception, http.RequestAborted);
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

    private static async IAsyncEnumerable<QueryStreamFrame> FaultFrames(
        IAsyncEnumerable<QueryStreamFrame> frames,
        ILogger logger,
        InfoCarrierRequestMeasurement? measurement,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using IAsyncEnumerator<QueryStreamFrame> enumerator = frames.GetAsyncEnumerator(cancellationToken);
        bool started = false;
        while (true)
        {
            QueryStreamFrame? frame = null;
            InfoCarrierFault? fault = null;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    yield break;
                }

                frame = enumerator.Current;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogQueryFailure(logger, exception);
                fault = InfoCarrierFaultMapper.Capture(exception);
            }

            if (fault is not null)
            {
                measurement?.FailFault(fault);
                if (!started)
                {
                    yield return new QueryStartFrame(QueryStreamFrameCodec.Version, 0, false, null);
                }

                yield return new QueryFaultFrame(fault);
                yield break;
            }

            started = true;
            if (frame is QueryFaultFrame returnedFault)
            {
                measurement?.FailFault(returnedFault.Fault);
            }
            yield return frame!;
        }
    }

    private static async IAsyncEnumerable<QueryStreamFrame> BufferedFrames(
        IInfoCarrierServer server,
        QueryDataRequest request,
        ILogger logger,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        QueryDataResult? result = null;
        InfoCarrierFault? fault = null;
        try
        {
            result = await server.QueryDataAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            fault = InfoCarrierFaultMapper.Capture(exception);
            LogQueryFailure(logger, exception);
        }

        if (fault is not null)
        {
            yield return new QueryStartFrame(QueryStreamFrameCodec.Version, 0, false, null);
            yield return new QueryFaultFrame(fault);
            yield break;
        }

        yield return new QueryStartFrame(
            QueryStreamFrameCodec.Version, 0, result!.IsEntityResult, result.ElementTypeName);

        List<DynamicValueNode> rows = JsonSerializer.Deserialize(
            result.SerializedResults, ExpressionJsonContext.Default.ListDynamicValueNode) ?? [];
        foreach (DynamicValueNode row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new QueryRowFrame(row);
        }

        foreach (ServerLogEvent log in result.ServerLog ?? [])
        {
            yield return new QueryLogFrame(log);
        }

        yield return new QueryCompleteFrame();
    }

    private static void LogQueryFailure(ILogger logger, Exception exception)
    {
        bool missingShape = exception.Data[typeof(Expressions.AnonymousShapeCatalog)] is true;
        Log(logger, LogLevel.Error, 35001,
            missingShape
                ? "InfoCarrier Query failed during operation execution: an anonymous shape is not registered in the server's trusted catalog. Reason={Reason}; ExceptionType={ExceptionType}."
                : "InfoCarrier Query failed during operation execution. Reason={Reason}; ExceptionType={ExceptionType}.",
            missingShape ? "AnonymousShapeNotRegistered" : "UnclassifiedFailure",
            exception.GetType().FullName);
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
