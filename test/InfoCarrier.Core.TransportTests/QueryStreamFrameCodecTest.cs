// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using InfoCarrier.Core.Wire;
using OldFrames = InfoCarrier.Core.Wire.V3.Query;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class QueryStreamFrameCodecTest
{
    [Fact]
    public async Task A_row_is_read_before_the_producer_completes()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipe = new Pipe();
        Task writing = Write();

        await using IAsyncEnumerator<QueryStreamFrame> reader = QueryStreamFrameCodec
            .ReadAsync(pipe.Reader.AsStream(), TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await reader.MoveNextAsync());
        Assert.IsType<QueryStartFrame>(reader.Current);
        Assert.True(await reader.MoveNextAsync());
        QueryRowFrame row = Assert.IsType<QueryRowFrame>(reader.Current);
        Assert.Equal(42, Assert.IsType<JsonElement>(row.Value.PrimitiveValue).GetInt32());
        Assert.False(writing.IsCompleted);

        release.TrySetResult();
        Assert.True(await reader.MoveNextAsync());
        Assert.IsType<QueryCompleteFrame>(reader.Current);
        Assert.False(await reader.MoveNextAsync());
        await writing;

        async Task Write()
        {
            await using Stream destination = pipe.Writer.AsStream();
            await QueryStreamFrameCodec.WriteAsync(destination, Frames(release.Task));
        }
    }

    [Fact]
    public async Task Logs_and_faults_round_trip_without_object_frames()
    {
        var fault = new InfoCarrierFault
        {
            TypeName = typeof(InvalidOperationException).FullName!,
            Message = "failed after a row",
        };
        var log = new ServerLogEvent
        {
            Level = 2,
            EventId = 17,
            EventName = "Streaming",
            Category = "Research",
            Message = "one row",
        };
        await using var stream = new MemoryStream();

        await QueryStreamFrameCodec.WriteAsync(stream, Frames(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        string json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.StartsWith($"[[0,{QueryStreamFrameCodec.Version},7,false,\"System.Int32\"],[2,", json, StringComparison.Ordinal);
        Assert.EndsWith("]]", json, StringComparison.Ordinal);

        stream.Position = 0;
        List<QueryStreamFrame> frames = await QueryStreamFrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Collection(
            frames,
            frame => Assert.IsType<QueryStartFrame>(frame),
            frame => Assert.Equal(log, Assert.IsType<QueryLogFrame>(frame).Event),
            frame => Assert.Equal(fault, Assert.IsType<QueryFaultFrame>(frame).Fault));

        async IAsyncEnumerable<QueryStreamFrame> Frames(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new QueryStartFrame(QueryStreamFrameCodec.Version, 7, false, "System.Int32");
            yield return new QueryLogFrame(log);
            yield return new QueryFaultFrame(fault);
            await Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("[]", "empty")]
    [InlineData("[[1,{}],[3]]", "start")]
    [InlineData("[[0,3,0,false,null]]", "terminal")]
    [InlineData("[[0,3,0,false,null],[3],[3]]", "after")]
    [InlineData("[[0,3,0,false,null],[99]]", "tag")]
    public async Task Historical_malformed_streams_are_rejected(string json, string expected)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        JsonException exception = await Assert.ThrowsAsync<JsonException>(async () =>
            await OldFrames.QueryStreamFrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Historical_codec_preserves_forwarded_log_identity_and_order()
    {
        await using var stream = new MemoryStream();
        var codec = new Protocol3WireProtocolCodec();
        var log = new ServerLogEvent
        {
            Level = 3, EventId = 17, EventName = "Forwarded", Category = "Test.Historical", Message = "one row",
        };
        await codec.WriteFramesAsync(stream, Frames(), TestContext.Current.CancellationToken);
        stream.Position = 0;
        List<OldFrames.QueryStreamFrame> frames = await OldFrames.QueryStreamFrameCodec.ReadAsync(
            stream, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, Assert.IsType<OldFrames.QueryStartFrame>(frames[0]).Version);
        var restored = Assert.IsType<OldFrames.QueryLogFrame>(frames[1]).Event;
        Assert.Equal(log.Level, restored.Level);
        Assert.Equal(log.EventId, restored.EventId);
        Assert.Equal(log.EventName, restored.EventName);
        Assert.Equal(log.Category, restored.Category);
        Assert.Equal(log.Message, restored.Message);
        Assert.IsType<OldFrames.QueryCompleteFrame>(frames[2]);
        Assert.Equal(3, frames.Count);

        async IAsyncEnumerable<QueryStreamFrame> Frames()
        {
            yield return new QueryStartFrame(QueryStreamFrameCodec.Version, 0, false, null);
            yield return new QueryLogFrame(log);
            yield return new QueryCompleteFrame();
            await Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("[]", "empty")]
    [InlineData("[[1,{}],[3]]", "start")]
    [InlineData("[[0,4,0,false,null]]", "terminal")]
    [InlineData("[[0,4,0,false,null],[3],[3]]", "after")]
    [InlineData("[[0,4,0,false,null],[99]]", "tag")]
    public async Task Malformed_streams_are_rejected(string json, string expected)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            async () => await QueryStreamFrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async IAsyncEnumerable<QueryStreamFrame> Frames(
        Task release,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new QueryStartFrame(QueryStreamFrameCodec.Version, 0, false, typeof(int).FullName);
        yield return new QueryRowFrame(new DynamicValueNode
        {
            Type = new TypeNode { Name = typeof(int).FullName! },
            PrimitiveValue = 42,
        });
        await release.WaitAsync(cancellationToken);
        yield return new QueryCompleteFrame();
    }
}
