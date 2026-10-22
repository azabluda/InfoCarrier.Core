// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
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
            .ReadAsync(pipe.Reader.AsStream())
            .GetAsyncEnumerator();
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

        await QueryStreamFrameCodec.WriteAsync(stream, Frames());
        string json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.StartsWith("[[0,1,7,false,\"System.Int32\"],[2,", json, StringComparison.Ordinal);
        Assert.EndsWith("]]", json, StringComparison.Ordinal);

        stream.Position = 0;
        List<QueryStreamFrame> frames = await QueryStreamFrameCodec.ReadAsync(stream).ToListAsync();
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
    [InlineData("[[0,2,0,false,null],[3]]", "version")]
    [InlineData("[[0,1,0,false,null]]", "terminal")]
    [InlineData("[[0,1,0,false,null],[3],[3]]", "after")]
    [InlineData("[[0,1,0,false,null],[99]]", "tag")]
    public async Task Malformed_streams_are_rejected(string json, string expected)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        JsonException exception = await Assert.ThrowsAsync<JsonException>(
            async () => await QueryStreamFrameCodec.ReadAsync(stream).ToListAsync());
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
