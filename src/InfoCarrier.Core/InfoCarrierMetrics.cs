// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

/// <summary>
///     The <see cref="Meter" /> this provider publishes, so an application can count its round
///     trips to the server at runtime.
/// </summary>
/// <remarks>
///     <para>
///         <b>It exists because "how many requests does this screen cost" had no answer.</b> The
///         cost of this provider is round trips, and until now the only way to count them was to
///         read the query and predict them. Two readers of the documentation asked the question
///         independently and neither could answer it from a running application.
///     </para>
///     <para>
///         <b>Metrics work without a host logger.</b> Buffered and streamed operations are measured by
///         <see cref="TransportInfoCarrierClient" />, which an application constructs itself and
///         which EF's service provider never sees. A <see cref="Meter" /> needs no wiring: name it
///         to <c>dotnet-counters</c>, to OpenTelemetry, or to a <see cref="MeterListener" /> in
///         the process, and the instruments below start reporting.
///     </para>
///     <para>
///         <b>Both instruments, on purpose.</b> The histogram carries a count as every collector
///         exposes it, so the counter is redundant to a metrics backend and is not redundant to a
///         developer, who asked "how many" and should not have to read a quantile to find out.
///     </para>
///     <para>
///         Nothing is measured while nothing is listening: the timestamp is not even taken.
///     </para>
///     <para>
///         <b>It measures what <see cref="TransportInfoCarrierClient" /> sends, so an application
///         that supplies its own <see cref="IInfoCarrierClient" /> publishes no client measurements here.</b>
///         A client measurement describes an attempt by this library's client, including failures
///         before sending. A substituted client owns its own measurements. The HTTP endpoint
///         adapter independently measures incoming requests with the same meter.
///     </para>
/// </remarks>
public static class InfoCarrierMetrics
{
    /// <summary>
    ///     The meter's name. Pass it to <c>dotnet-counters</c>, to
    ///     <c>AddMeter</c> in OpenTelemetry, or to a <see cref="MeterListener" />.
    /// </summary>
    public const string MeterName = "InfoCarrier.Core";

    /// <summary>
    ///     The name of the counter of completed round trips, successful and failed alike.
    /// </summary>
    public const string RoundTripCountInstrumentName = "infocarrier.client.round_trips";

    /// <summary>
    ///     The name of the histogram of round-trip duration, in seconds.
    /// </summary>
    public const string RoundTripDurationInstrumentName = "infocarrier.client.round_trip.duration";

    /// <summary>
    ///     The tag naming which of the nine operations a measurement belongs to.
    /// </summary>
    public const string OperationTagName = "infocarrier.operation";

    /// <summary>
    ///     The tag naming the exception type, present only on a round trip that failed. The name
    ///     follows OpenTelemetry's convention so that a collector groups it with everything else.
    /// </summary>
    public const string ErrorTypeTagName = "error.type";

    /// <summary>The configured client name, empty for an unnamed registration.</summary>
    public const string ClientNameTagName = "infocarrier.client.name";

    /// <summary>The client registration number within its host, starting at one.</summary>
    public const string ClientRegistrationTagName = "infocarrier.client.registration";

    /// <summary>The configured server name, empty for an unnamed registration.</summary>
    public const string EndpointNameTagName = "infocarrier.endpoint.name";

    /// <summary>The server registration number within its host, starting at one.</summary>
    public const string EndpointRegistrationTagName = "infocarrier.endpoint.registration";

    /// <summary>The result: success, error, cancelled, or incomplete.</summary>
    public const string OutcomeTagName = "infocarrier.outcome";

    /// <summary>The counter of completed HTTP endpoint requests.</summary>
    public const string ServerRequestCountInstrumentName = "infocarrier.server.requests";

    /// <summary>The histogram of HTTP endpoint request durations, in seconds.</summary>
    public const string ServerRequestDurationInstrumentName = "infocarrier.server.request.duration";

    private static readonly Meter Meter = new(
        MeterName,
        typeof(InfoCarrierMetrics).Assembly.GetName().Version?.ToString());

    private static readonly Counter<long> RoundTrips = Meter.CreateCounter<long>(
        RoundTripCountInstrumentName,
        unit: "{round_trip}",
        description: "Round trips to the InfoCarrier server, successful and failed alike.");

    private static readonly Histogram<double> RoundTripDuration = Meter.CreateHistogram<double>(
        RoundTripDurationInstrumentName,
        unit: "s",
        description: "How long a round trip to the InfoCarrier server took.");

    private static readonly Counter<long> ServerRequests = Meter.CreateCounter<long>(
        ServerRequestCountInstrumentName, unit: "{request}", description: "Completed InfoCarrier HTTP endpoint requests.");

    private static readonly Histogram<double> ServerRequestDuration = Meter.CreateHistogram<double>(
        ServerRequestDurationInstrumentName, unit: "s", description: "InfoCarrier HTTP endpoint request duration.");

    /// <summary>
    ///     Starts an attempt only when a client instrument has listeners. Otherwise no timestamp
    ///     or measurement allocation is needed.
    /// </summary>
    internal static InfoCarrierRequestMeasurement? StartClient(InfoCarrierMetricIdentity? identity, InfoCarrierOperation operation)
        => RoundTrips.Enabled || RoundTripDuration.Enabled
            ? new(RoundTrips, RoundTripDuration, identity, server: false, operation)
            : null;

    internal static InfoCarrierRequestMeasurement? StartEndpoint(InfoCarrierMetricIdentity? identity)
        => ServerRequests.Enabled || ServerRequestDuration.Enabled
            ? new(ServerRequests, ServerRequestDuration, identity, server: true, operation: null)
            : null;

    // A constant per value rather than `ToString()`: an enum's `ToString` allocates and searches
    // its metadata on every call, and this one runs on every round trip.
    internal static string NameOf(InfoCarrierOperation? operation)
        => operation switch
        {
            InfoCarrierOperation.Query => nameof(InfoCarrierOperation.Query),
            InfoCarrierOperation.SaveChanges => nameof(InfoCarrierOperation.SaveChanges),
            InfoCarrierOperation.BeginTransaction => nameof(InfoCarrierOperation.BeginTransaction),
            InfoCarrierOperation.CommitTransaction => nameof(InfoCarrierOperation.CommitTransaction),
            InfoCarrierOperation.RollbackTransaction => nameof(InfoCarrierOperation.RollbackTransaction),
            InfoCarrierOperation.CreateSavepoint => nameof(InfoCarrierOperation.CreateSavepoint),
            InfoCarrierOperation.RollbackToSavepoint => nameof(InfoCarrierOperation.RollbackToSavepoint),
            InfoCarrierOperation.ReleaseSavepoint => nameof(InfoCarrierOperation.ReleaseSavepoint),
            InfoCarrierOperation.SupportsSavepoints => nameof(InfoCarrierOperation.SupportsSavepoints),
            _ => "Unknown",
        };
}

// Allocated only when the corresponding instruments have listeners. One owner closes the
// measurement after response disposal, including early iterator disposal; no ambient selection.
internal sealed class InfoCarrierRequestMeasurement(
    Counter<long> count,
    Histogram<double> duration,
    InfoCarrierMetricIdentity? identity,
    bool server,
    InfoCarrierOperation? operation) : IDisposable
{
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private int _disposed;
    private string? _errorType;
    private string _outcome = "incomplete";
    internal InfoCarrierOperation? Operation { get; set; } = operation;

    internal void Complete()
    {
        if (_outcome == "incomplete") { _outcome = "success"; }
    }

    internal void Fail(Exception exception, CancellationToken cancellationToken)
    {
        _errorType = exception.GetType().FullName;
        _outcome = cancellationToken.IsCancellationRequested && exception is OperationCanceledException or IOException
            ? "cancelled" : "error";
    }

    internal void FailFault(InfoCarrierFault fault)
    {
        _errorType = InfoCarrierFaultMapper.GetMetricExceptionTypeName(fault);
        _outcome = "error";
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
        double elapsedSeconds = Stopwatch.GetElapsedTime(_startedAt).TotalSeconds;
        TagList tags = default;
        tags.Add(InfoCarrierMetrics.OperationTagName, InfoCarrierMetrics.NameOf(Operation));
        tags.Add(InfoCarrierMetrics.OutcomeTagName, _outcome);
        if (identity is not null)
        {
            tags.Add(server ? InfoCarrierMetrics.EndpointNameTagName : InfoCarrierMetrics.ClientNameTagName, identity.Name);
            tags.Add(server ? InfoCarrierMetrics.EndpointRegistrationTagName : InfoCarrierMetrics.ClientRegistrationTagName,
                identity.Registration);
        }
        if (_errorType is not null) { tags.Add(InfoCarrierMetrics.ErrorTypeTagName, _errorType); }
        count.Add(1, tags);
        duration.Record(elapsedSeconds, tags);
    }
}
