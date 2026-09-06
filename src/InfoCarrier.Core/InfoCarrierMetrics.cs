// Licensed under the MIT license. See license.txt file in the project root for license information.

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
///         <b>Metrics rather than a log event, because the round trip happens where there is no
///         logger.</b> Every operation funnels through one method on
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
///         that supplies its own <see cref="IInfoCarrierClient" /> publishes nothing here.</b>
///         That is the honest boundary: a measurement means "a request this library put on the
///         wire", and a substituted client sends requests this library never saw.
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

    /// <summary>
    ///     Whether anything is listening. False on an application that never named the meter, and
    ///     the reason a round trip costs nothing extra there.
    /// </summary>
    internal static bool Enabled
        => RoundTrips.Enabled || RoundTripDuration.Enabled;

    /// <summary>
    ///     Records one completed round trip.
    /// </summary>
    /// <param name="operation">Which of the nine operations it was.</param>
    /// <param name="elapsed">How long it took.</param>
    /// <param name="errorType">
    ///     The full name of the exception that ended it, or <see langword="null" /> when it
    ///     succeeded.
    /// </param>
    internal static void RoundTripCompleted(
        InfoCarrierOperation operation,
        TimeSpan elapsed,
        string? errorType)
    {
        var operationTag = new KeyValuePair<string, object?>(OperationTagName, NameOf(operation));

        if (errorType is null)
        {
            RoundTrips.Add(1, operationTag);
            RoundTripDuration.Record(elapsed.TotalSeconds, operationTag);
            return;
        }

        var errorTag = new KeyValuePair<string, object?>(ErrorTypeTagName, errorType);

        RoundTrips.Add(1, operationTag, errorTag);
        RoundTripDuration.Record(elapsed.TotalSeconds, operationTag, errorTag);
    }

    // A constant per value rather than `ToString()`: an enum's `ToString` allocates and searches
    // its metadata on every call, and this one runs on every round trip.
    private static string NameOf(InfoCarrierOperation operation)
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
            _ => operation.ToString(),
        };
}
