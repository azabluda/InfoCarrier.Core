// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Text.Json;
using InfoCarrier.Core.Common;
using Xunit;
using Xunit.Abstractions;

namespace InfoCarrier.Core.TransportTests;

[Collection(nameof(RoundTripMetricsCollection))]
public class StreamingEnvelopeResearchTest(ITestOutputHelper output)
{
    private const int PayloadBytes = 8 * 1024 * 1024;
    private const int Iterations = 5;

    private static readonly SystemTextJsonInfoCarrierSerializer Serializer = new();

    [Fact]
    public async Task Compare_buffered_and_streamed_outer_envelopes()
    {
        byte[] payload = GC.AllocateUninitializedArray<byte>(PayloadBytes);
        new Random(42).NextBytes(payload);
        var envelope = new InfoCarrierEnvelope
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = InfoCarrierOperation.Query,
            Payload = payload,
        };

        byte[] wire = Serializer.Serialize(envelope);

        await WriteStreamed(envelope);
        _ = Serializer.Deserialize<InfoCarrierEnvelope>(wire);
        await ReadStreamed(wire);

        Measurement bufferedWrite = await Measure(
            () =>
            {
                byte[] bytes = Serializer.Serialize(envelope);
                GC.KeepAlive(bytes);
                return ValueTask.CompletedTask;
            });

        Measurement streamedWrite = await Measure(
            async () => _ = await WriteStreamed(envelope));

        Measurement bufferedRead = await Measure(
            () =>
            {
                InfoCarrierEnvelope? result = Serializer.Deserialize<InfoCarrierEnvelope>(wire);
                GC.KeepAlive(result);
                return ValueTask.CompletedTask;
            });

        Measurement streamedRead = await Measure(
            async () => _ = await ReadStreamed(wire));

        output.WriteLine($"Payload bytes: {payload.Length:N0}");
        output.WriteLine($"Wire bytes: {wire.Length:N0}");
        Report("Buffered write", bufferedWrite);
        Report("Streamed write", streamedWrite);
        Report("Buffered read", bufferedRead);
        Report("Streamed read", streamedRead);

        Assert.Equal(wire.Length, await WriteStreamed(envelope));
        InfoCarrierEnvelope? decoded = await ReadStreamed(wire);
        Assert.NotNull(decoded);
        Assert.Equal(payload, decoded.Payload);
    }

    private static async ValueTask<long> WriteStreamed(InfoCarrierEnvelope envelope)
    {
        var destination = new CountingWriteStream();
        await JsonSerializer.SerializeAsync(
            destination,
            envelope,
            InfoCarrierJsonContext.Default.InfoCarrierEnvelope).ConfigureAwait(false);
        return destination.BytesWritten;
    }

    private static async ValueTask<InfoCarrierEnvelope?> ReadStreamed(byte[] wire)
    {
        using var source = new MemoryStream(wire, writable: false);
        return await JsonSerializer.DeserializeAsync(
            source,
            InfoCarrierJsonContext.Default.InfoCarrierEnvelope).ConfigureAwait(false);
    }

    private static async Task<Measurement> Measure(Func<ValueTask> operation)
    {
        var elapsed = new double[Iterations];
        var allocated = new long[Iterations];

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetTotalAllocatedBytes(precise: true);
            long started = Stopwatch.GetTimestamp();
            await operation().ConfigureAwait(false);
            elapsed[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            allocated[iteration] = GC.GetTotalAllocatedBytes(precise: true) - before;
        }

        Array.Sort(elapsed);
        Array.Sort(allocated);
        return new Measurement(
            elapsed[Iterations / 2],
            elapsed[0],
            elapsed[^1],
            allocated[Iterations / 2],
            allocated[0],
            allocated[^1]);
    }

    private void Report(string name, Measurement measurement)
        => output.WriteLine(
            $"{name}: {measurement.ElapsedMedian:N2} ms median "
            + $"[{measurement.ElapsedMinimum:N2}, {measurement.ElapsedMaximum:N2}], "
            + $"{measurement.AllocatedMedian:N0} allocated bytes median "
            + $"[{measurement.AllocatedMinimum:N0}, {measurement.AllocatedMaximum:N0}]");

    private sealed class CountingWriteStream : Stream
    {
        public long BytesWritten { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;

        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => BytesWritten += count;

        public override void Write(ReadOnlySpan<byte> buffer) => BytesWritten += buffer.Length;

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            BytesWritten += buffer.Length;
            return ValueTask.CompletedTask;
        }
    }

    private sealed record Measurement(
        double ElapsedMedian,
        double ElapsedMinimum,
        double ElapsedMaximum,
        long AllocatedMedian,
        long AllocatedMinimum,
        long AllocatedMaximum);
}
