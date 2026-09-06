// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.Metrics;
using InfoCarrier.Core.Common;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     The meter is process-wide, so a listener attached here would also see round trips made by
///     any test class running beside it. This collection runs alone.
/// </summary>
[CollectionDefinition(nameof(RoundTripMetricsCollection), DisableParallelization = true)]
public class RoundTripMetricsCollection;

[Collection(nameof(RoundTripMetricsCollection))]
public class RoundTripMetricsTest
{
    private static readonly IInfoCarrierSerializer Serializer = new SystemTextJsonInfoCarrierSerializer();

    [Fact]
    public async Task It_counts_one_round_trip_per_operation_and_names_which()
    {
        // THE QUESTION THIS ANSWERS IS "how many requests does this screen cost". Two readers of
        // the documentation asked it and neither could answer it from a running application.
        List<Recorded> recorded = await Measure(async client =>
        {
            Assert.True(await client.SupportsSavepointsAsync("t1"));
            Assert.True(await client.SupportsSavepointsAsync("t1"));
            await client.CommitTransactionAsync("t1");
        });

        Assert.Equal(
            3,
            recorded.Where(r => r.Instrument == InfoCarrierMetrics.RoundTripCountInstrumentName)
                .Sum(r => (long)r.Value));

        Assert.Equal(
            2,
            recorded.Count(r => r.Instrument == InfoCarrierMetrics.RoundTripCountInstrumentName
                && r.Operation == nameof(InfoCarrierOperation.SupportsSavepoints)));

        Assert.Single(
            recorded,
            r => r.Instrument == InfoCarrierMetrics.RoundTripCountInstrumentName
                && r.Operation == nameof(InfoCarrierOperation.CommitTransaction));

        // The duration histogram sees every one of them, and no measurement is negative.
        List<Recorded> durations =
            [.. recorded.Where(r => r.Instrument == InfoCarrierMetrics.RoundTripDurationInstrumentName)];

        Assert.Equal(3, durations.Count);
        Assert.All(durations, d => Assert.True(d.Value >= 0));

        // Nothing failed, so nothing carries an error tag.
        Assert.All(recorded, r => Assert.Null(r.ErrorType));
    }

    [Fact]
    public async Task A_failed_round_trip_is_counted_and_carries_the_exception_type()
    {
        // Counting only the successes would understate exactly the case a reader is investigating:
        // a screen that is slow because its requests are failing and being retried.
        List<Recorded> recorded = await Measure(
            async client => await Assert.ThrowsAsync<TimeoutException>(
                () => client.CommitTransactionAsync("t1")),
            _ => throw new TimeoutException("the server did not answer"));

        Recorded count = Assert.Single(
            recorded, r => r.Instrument == InfoCarrierMetrics.RoundTripCountInstrumentName);

        Assert.Equal(1, (long)count.Value);
        Assert.Equal(nameof(InfoCarrierOperation.CommitTransaction), count.Operation);
        Assert.Equal(typeof(TimeoutException).FullName, count.ErrorType);
    }

    private static async Task<List<Recorded>> Measure(
        Func<IInfoCarrierClient, Task> work,
        Func<InfoCarrierEnvelope, InfoCarrierEnvelope>? respond = null)
    {
        var recorded = new List<Recorded>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == InfoCarrierMetrics.MeterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, value, tags, _) => recorded.Add(Recorded.From(instrument, value, tags)));
        listener.SetMeasurementEventCallback<double>(
            (instrument, value, tags, _) => recorded.Add(Recorded.From(instrument, value, tags)));
        listener.Start();

        var client = new TransportInfoCarrierClient(
            new StubTransport(respond ?? Answer), Serializer);

        await work(client);

        return recorded;
    }

    private static InfoCarrierEnvelope Answer(InfoCarrierEnvelope request)
        => new()
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = request.Operation,
            Payload = request.Operation == InfoCarrierOperation.SupportsSavepoints
                ? Serializer.Serialize(true)
                : Serializer.Serialize<object?>(null),
        };

    private sealed record Recorded(string Instrument, double Value, string? Operation, string? ErrorType)
    {
        public static Recorded From(
            Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            string? operation = null;
            string? errorType = null;

            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == InfoCarrierMetrics.OperationTagName)
                {
                    operation = tag.Value as string;
                }
                else if (tag.Key == InfoCarrierMetrics.ErrorTypeTagName)
                {
                    errorType = tag.Value as string;
                }
            }

            return new Recorded(instrument.Name, value, operation, errorType);
        }
    }

    private sealed class StubTransport(Func<InfoCarrierEnvelope, InfoCarrierEnvelope> respond)
        : IInfoCarrierTransport
    {
        public Task<InfoCarrierEnvelope> SendAsync(
            InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => Task.FromResult(respond(request));
    }
}
