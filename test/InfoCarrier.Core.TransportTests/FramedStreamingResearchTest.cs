// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.IO.Pipelines;
using System.Net;
using System.Data;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text.Json;
using System.Buffers;
using InfoCarrier.Core.Expressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;
using Xunit.Abstractions;

namespace InfoCarrier.Core.TransportTests;

[Collection(nameof(RoundTripMetricsCollection))]
public class FramedStreamingResearchTest(ITestOutputHelper output, NorthwindServerFactory factory)
    : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task Tracked_many_to_many_frames_preserve_loaded_collections_and_cross_frame_references()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StreamingGraphContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new StreamingGraphContext(options);
        await context.Database.EnsureCreatedAsync();

        var shared = new StreamingTag { Name = "shared" };
        context.AddRange(
            new StreamingPost { Title = "first", Tags = [shared, new StreamingTag { Name = "first-only" }] },
            new StreamingPost { Title = "second", Tags = [shared] });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var mapper = new DynamicValueMapper(
            context.Model,
            new TypeNodeMapper(context.Model),
            new TypeNodeResolver(context.Model));
        var frames = new List<DynamicValueNode>();

        await foreach (StreamingPost post in context.Posts
            .Include(post => post.Tags)
            .OrderBy(post => post.Id)
            .AsAsyncEnumerable())
        {
            Assert.True(context.Entry(post).Collection(candidate => candidate.Tags).IsLoaded);
            DynamicValueNode node = MapRow(post);
            byte[] frame = JsonSerializer.SerializeToUtf8Bytes(node);
            frames.Add(JsonSerializer.Deserialize<DynamicValueNode>(frame)!);
        }

        Assert.Equal(2, frames.Count);
        DynamicPropertyValue firstTags = Assert.Single(
            frames[0].Properties,
            property => property.Name == nameof(StreamingPost.Tags) && !property.IsJoinRows);
        Assert.True(firstTags.IsLoadedNavigation);
        Assert.Equal(2, firstTags.Value!.Items!.Count);
        Assert.Contains(frames[0].Properties, property => property.IsJoinRows);

        HashSet<int> firstIds = [.. Descendants(frames[0]).Select(node => node.Id).Where(id => id != 0)];
        Assert.Contains(Descendants(frames[1]), node => node.Ref is { } reference && firstIds.Contains(reference));

        DynamicValueNode MapRow(StreamingPost post)
            => mapper.ToRowValue(
                post,
                post.GetType(),
                (candidate, navigation) => !navigation.IsShadowProperty()
                    && context.Entry(candidate).Navigation(navigation.Name).IsLoaded,
                candidate => context.ChangeTracker.Entries()
                    .Any(entry => ReferenceEquals(entry.Entity, candidate)),
                (candidate, property) => context.Entry(candidate).Property(property.Name).CurrentValue,
                ReadJoinEntities,
                candidate => context.ChangeTracker.Entries()
                    .FirstOrDefault(entry => ReferenceEquals(entry.Entity, candidate))
                    ?.Metadata
                    ?? context.Model.FindEntityType(candidate.GetType()));

        IEnumerable<object> ReadJoinEntities(object owner, ISkipNavigation skip, bool loaded)
        {
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry ownerEntry = context.Entry(owner);
            object?[] ownerKey = skip.ForeignKey.PrincipalKey.Properties
                .Select(property => ownerEntry.Property(property.Name).CurrentValue)
                .ToArray();

            return context.ChangeTracker.Entries()
                .Where(candidate => candidate.Metadata == skip.JoinEntityType)
                .Where(candidate => skip.ForeignKey.Properties
                    .Select((property, index) => Equals(
                        candidate.Property(property.Name).CurrentValue,
                        ownerKey[index]))
                    .All(matches => matches))
                .Select(candidate => candidate.Entity);
        }

        static IEnumerable<DynamicValueNode> Descendants(DynamicValueNode root)
        {
            yield return root;
            foreach (DynamicValueNode child in root.Properties
                .Select(property => property.Value)
                .Where(value => value is not null)
                .Cast<DynamicValueNode>()
                .Concat(root.Items ?? []))
            {
                foreach (DynamicValueNode descendant in Descendants(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    [Fact]
    public async Task Real_entity_and_projection_nodes_flow_through_the_framed_response()
    {
        using HttpClient warmup = factory.CreateClient();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new NodeFramedHandler(ProviderNodes, release);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "query"),
            HttpCompletionOption.ResponseHeadersRead);

        await using IAsyncEnumerator<NodeFrame> frames = ReadNodeFrames(response).GetAsyncEnumerator();
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(FrameKind.Start, frames.Current.Kind);
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(FrameKind.Row, frames.Current.Kind);
        Assert.NotNull(frames.Current.Node?.EntityKey);
        Assert.NotEmpty(frames.Current.Node.Properties);
        Assert.False(handler.Completed.Task.IsCompleted);

        release.TrySetResult();

        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(FrameKind.Row, frames.Current.Kind);
        Assert.NotNull(frames.Current.Node?.Items);
        Assert.Equal(2, frames.Current.Node.Items.Count);
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(FrameKind.Complete, frames.Current.Kind);
        Assert.False(await frames.MoveNextAsync());
        await handler.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        async IAsyncEnumerable<DynamicValueNode> ProviderNodes(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using IServiceScope scope = factory.Services.CreateScope();
            await using NorthwindContext context = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
            var mapper = new DynamicValueMapper(
                context.Model,
                new TypeNodeMapper(context.Model),
                new TypeNodeResolver(context.Model));
            OrderDetail entity = await context.OrderDetails
                .OrderBy(detail => detail.OrderId)
                .ThenBy(detail => detail.ProductId)
                .FirstAsync(cancellationToken);

            yield return mapper.ToRowValue(
                entity,
                entity.GetType(),
                IsLoaded,
                candidate => context.ChangeTracker.Entries().Any(entry => ReferenceEquals(entry.Entity, candidate)),
                (candidate, property) => context.Entry(candidate).Property(property.Name).CurrentValue,
                (_, _, _) => [],
                candidate => context.Model.FindEntityType(candidate.GetType()));

            mapper.ResetReferenceScope();
            yield return mapper.ToDynamicValue(
                new object[] { entity.OrderId, entity.ProductId },
                typeof(object[]));

            bool IsLoaded(object candidate, INavigationBase navigation)
                => !navigation.IsShadowProperty()
                    && context.Entry(candidate).Navigation(navigation.Name).IsLoaded;
        }
    }

    [Fact]
    public async Task Real_provider_rows_flow_through_the_framed_response()
    {
        using HttpClient warmup = factory.CreateClient();
        int expectedCount;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            await using NorthwindContext context = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
            expectedCount = await context.OrderDetails.CountAsync();
        }

        var providerDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FramedHandler(rows: ProviderRows);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "query"),
            HttpCompletionOption.ResponseHeadersRead);

        await using IAsyncEnumerator<int> rows = ReadRows(response).GetAsyncEnumerator();
        Assert.True(await rows.MoveNextAsync());
        Assert.Equal(1, rows.Current);
        Assert.False(providerDisposed.Task.IsCompleted);

        handler.Release.TrySetResult();

        int count = 1;
        while (await rows.MoveNextAsync())
        {
            count++;
        }

        Assert.Equal(expectedCount, count);
        await providerDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        async IAsyncEnumerable<int> ProviderRows(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using IServiceScope scope = factory.Services.CreateScope();
            await using NorthwindContext context = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
            try
            {
                await foreach (int orderId in context.OrderDetails
                    .AsNoTracking()
                    .OrderBy(detail => detail.OrderId)
                    .ThenBy(detail => detail.ProductId)
                    .Select(detail => detail.OrderId)
                    .AsAsyncEnumerable()
                    .WithCancellation(cancellationToken))
                {
                    yield return orderId;
                }
            }
            finally
            {
                providerDisposed.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task Measure_real_projection_wire_nodes()
    {
        using HttpClient warmup = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        await using NorthwindContext context = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
        object[][] projections = await context.OrderDetails
            .AsNoTracking()
            .OrderBy(detail => detail.OrderId)
            .ThenBy(detail => detail.ProductId)
            .Select(detail => new object[]
            {
                detail.OrderId,
                detail.ProductId,
                detail.UnitPrice,
                detail.Quantity,
            })
            .Take(1_000)
            .ToArrayAsync();
        var mapper = new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver());
        DynamicValueNode[] nodes = projections
            .Select(projection => mapper.ToDynamicValue(projection, typeof(object[])))
            .ToArray();

        _ = WriteBufferedNodes(nodes);
        _ = WriteCompactNodes(nodes);
        Measurement buffered = await Measure(() => ValueTask.FromResult(WriteBufferedNodes(nodes)));
        Measurement compact = await Measure(() => ValueTask.FromResult(WriteCompactNodes(nodes)));

        output.WriteLine($"Real projection nodes: {nodes.Length:N0}");
        output.WriteLine($"Buffered: {buffered.Bytes:N0} bytes, {buffered.Milliseconds:N2} ms, "
            + $"{buffered.AllocatedBytes:N0} allocated bytes");
        output.WriteLine($"Compact frames: {compact.Bytes:N0} bytes, {compact.Milliseconds:N2} ms, "
            + $"{compact.AllocatedBytes:N0} allocated bytes");

        Assert.True(nodes.Length >= 100);
        Assert.True(buffered.Bytes > 0);
        Assert.True(compact.Bytes > buffered.Bytes);
    }

    [Fact]
    public async Task Early_disposal_releases_the_real_provider_context()
    {
        using HttpClient warmup = factory.CreateClient();
        var providerDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionOpened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FramedHandler(rows: ProviderRows);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "query"),
            HttpCompletionOption.ResponseHeadersRead);

        await using (IAsyncEnumerator<int> rows = ReadRows(response).GetAsyncEnumerator())
        {
            Assert.True(await rows.MoveNextAsync());
        }

        response.Dispose();
        await handler.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await providerDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await connectionOpened.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await connectionClosed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(handler.Completed.Task.IsCompleted);

        async IAsyncEnumerable<int> ProviderRows(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using IServiceScope scope = factory.Services.CreateScope();
            await using NorthwindContext context = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
            context.Database.GetDbConnection().StateChange += (_, change) =>
            {
                if (change.CurrentState == ConnectionState.Open)
                {
                    connectionOpened.TrySetResult();
                }
                else if (change.CurrentState == ConnectionState.Closed)
                {
                    connectionClosed.TrySetResult();
                }
            };
            try
            {
                await foreach (int orderId in context.OrderDetails
                    .AsNoTracking()
                    .OrderBy(detail => detail.OrderId)
                    .Select(detail => detail.OrderId)
                    .AsAsyncEnumerable()
                    .WithCancellation(cancellationToken))
                {
                    yield return orderId;
                }
            }
            finally
            {
                providerDisposed.TrySetResult();
            }
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(10_000)]
    public async Task Measure_buffered_and_framed_flat_results(int rowCount)
    {
        int[] rows = Enumerable.Range(0, rowCount).ToArray();

        _ = JsonSerializer.SerializeToUtf8Bytes(rows);
        await WriteResult(rows);

        Measurement buffered = await Measure(
            () =>
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(rows);
                return ValueTask.FromResult((long)bytes.Length);
            });
        Measurement framed = await Measure(() => WriteResult(rows));
        Measurement compact = await Measure(() => WriteCompactResult(rows));

        output.WriteLine($"Rows: {rowCount:N0}");
        output.WriteLine($"Buffered: {buffered.Bytes:N0} bytes, {buffered.Milliseconds:N2} ms, "
            + $"{buffered.AllocatedBytes:N0} allocated bytes");
        output.WriteLine($"Framed: {framed.Bytes:N0} bytes, {framed.Milliseconds:N2} ms, "
            + $"{framed.AllocatedBytes:N0} allocated bytes");
        output.WriteLine($"Compact: {compact.Bytes:N0} bytes, {compact.Milliseconds:N2} ms, "
            + $"{compact.AllocatedBytes:N0} allocated bytes");

        Assert.True(buffered.Bytes > 0);
        Assert.True(framed.Bytes > 0);
        Assert.True(compact.Bytes > 0);
    }

    [Fact]
    public async Task A_row_reaches_the_client_before_the_server_completes()
    {
        var handler = new FramedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var request = new HttpRequestMessage(HttpMethod.Post, "query");
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);

        await using IAsyncEnumerator<Frame> frames = ReadFrames(response).GetAsyncEnumerator();

        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(FrameKind.Start, frames.Current.Kind);
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(1, frames.Current.Value);
        Assert.False(handler.Completed.Task.IsCompleted);

        handler.Release.TrySetResult();

        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(2, frames.Current.Value);
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(FrameKind.Log, frames.Current.Kind);
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(FrameKind.Complete, frames.Current.Kind);
        Assert.False(await frames.MoveNextAsync());
        await handler.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_fault_after_a_row_throws_during_enumeration()
    {
        var handler = new FramedHandler(fail: true);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "query"),
            HttpCompletionOption.ResponseHeadersRead);

        await using IAsyncEnumerator<int> rows = ReadRows(response).GetAsyncEnumerator();
        Assert.True(await rows.MoveNextAsync());
        Assert.Equal(1, rows.Current);

        handler.Release.TrySetResult();

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await rows.MoveNextAsync().AsTask());
        Assert.Equal("provider failed after row 1", failure.Message);
    }

    [Fact]
    public async Task Disposing_early_cancels_the_server_producer()
    {
        var handler = new FramedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "query"),
            HttpCompletionOption.ResponseHeadersRead);

        await using (IAsyncEnumerator<int> rows = ReadRows(response).GetAsyncEnumerator())
        {
            Assert.True(await rows.MoveNextAsync());
        }

        response.Dispose();
        await handler.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(handler.Completed.Task.IsCompleted);
    }

    private static async IAsyncEnumerable<Frame> ReadFrames(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await foreach (Frame? frame in JsonSerializer.DeserializeAsyncEnumerable<Frame>(
            body,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return frame ?? throw new InvalidOperationException("The stream contained a null frame.");
        }
    }

    private static async IAsyncEnumerable<int> ReadRows(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (Frame frame in ReadFrames(response, cancellationToken).ConfigureAwait(false))
        {
            if (frame.Kind == FrameKind.Row)
            {
                yield return frame.Value!.Value;
            }
            else if (frame.Kind == FrameKind.Fault)
            {
                throw new InvalidOperationException(frame.Message);
            }
        }
    }

    private static async IAsyncEnumerable<NodeFrame> ReadNodeFrames(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await foreach (NodeFrame? frame in JsonSerializer.DeserializeAsyncEnumerable<NodeFrame>(
            body,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return frame ?? throw new InvalidOperationException("The stream contained a null frame.");
        }
    }

    private enum FrameKind
    {
        Start,
        Row,
        Log,
        Complete,
        Fault,
    }

    private sealed record Frame(FrameKind Kind, int? Value = null, string? Message = null);

    private sealed record NodeFrame(FrameKind Kind, DynamicValueNode? Node = null);

    private static async ValueTask<long> WriteResult(int[] rows)
    {
        var destination = new CountingWriteStream();
        await destination.WriteAsync("["u8.ToArray()).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(destination, new Frame(FrameKind.Start)).ConfigureAwait(false);
        foreach (int row in rows)
        {
            await destination.WriteAsync(","u8.ToArray()).ConfigureAwait(false);
            await JsonSerializer.SerializeAsync(destination, new Frame(FrameKind.Row, row)).ConfigureAwait(false);
        }

        await destination.WriteAsync(","u8.ToArray()).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(destination, new Frame(FrameKind.Complete)).ConfigureAwait(false);
        await destination.WriteAsync("]"u8.ToArray()).ConfigureAwait(false);
        return destination.BytesWritten;
    }

    private static ValueTask<long> WriteCompactResult(int[] rows)
    {
        var destination = new CountingBufferWriter();
        using var writer = new Utf8JsonWriter(destination);
        writer.WriteStartArray();
        WriteTag(writer, 0);
        foreach (int row in rows)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(1);
            writer.WriteNumberValue(row);
            writer.WriteEndArray();
        }

        WriteTag(writer, 3);
        writer.WriteEndArray();
        writer.Flush();
        return ValueTask.FromResult(destination.BytesWritten);

        static void WriteTag(Utf8JsonWriter writer, int tag)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(tag);
            writer.WriteEndArray();
        }
    }

    private static long WriteBufferedNodes(DynamicValueNode[] nodes)
    {
        var destination = new CountingBufferWriter();
        using var writer = new Utf8JsonWriter(destination);
        JsonSerializer.Serialize(writer, nodes.ToList(), ExpressionJsonContext.Default.ListDynamicValueNode);
        writer.Flush();
        return destination.BytesWritten;
    }

    private static long WriteCompactNodes(DynamicValueNode[] nodes)
    {
        var destination = new CountingBufferWriter();
        using var writer = new Utf8JsonWriter(destination);
        writer.WriteStartArray();
        writer.WriteStartArray();
        writer.WriteNumberValue(0);
        writer.WriteEndArray();
        foreach (DynamicValueNode node in nodes)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(1);
            JsonSerializer.Serialize(writer, node, ExpressionJsonContext.Default.DynamicValueNode);
            writer.WriteEndArray();
        }

        writer.WriteStartArray();
        writer.WriteNumberValue(3);
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return destination.BytesWritten;
    }

    private static async Task<Measurement> Measure(Func<ValueTask<long>> operation)
    {
        const int iterations = 5;
        var times = new double[iterations];
        var allocations = new long[iterations];
        var sizes = new long[iterations];

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalAllocatedBytes(precise: true);
            long started = Stopwatch.GetTimestamp();
            sizes[iteration] = await operation().ConfigureAwait(false);
            times[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            allocations[iteration] = GC.GetTotalAllocatedBytes(precise: true) - before;
        }

        Array.Sort(times);
        Array.Sort(allocations);
        Array.Sort(sizes);
        return new Measurement(sizes[iterations / 2], times[iterations / 2], allocations[iterations / 2]);
    }

    private sealed record Measurement(long Bytes, double Milliseconds, long AllocatedBytes);

    private sealed class FramedHandler(
        bool fail = false,
        Func<CancellationToken, IAsyncEnumerable<int>>? rows = null) : HttpMessageHandler
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var pipe = new Pipe();
            _ = Produce(pipe.Writer, lifetime.Token);

            Stream body = new CancelOnDisposeStream(pipe.Reader.AsStream(), lifetime);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body),
            });
        }

        private async Task Produce(PipeWriter writer, CancellationToken cancellationToken)
        {
            try
            {
                await using Stream destination = writer.AsStream();
                await destination.WriteAsync("["u8.ToArray(), cancellationToken).ConfigureAwait(false);
                await Write(destination, new Frame(FrameKind.Start), first: true, cancellationToken).ConfigureAwait(false);

                if (rows is not null)
                {
                    bool firstRow = true;
                    await foreach (int row in rows(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        await Write(destination, new Frame(FrameKind.Row, row), first: false, cancellationToken)
                            .ConfigureAwait(false);
                        if (firstRow)
                        {
                            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                            firstRow = false;
                        }
                    }

                    await Write(destination, new Frame(FrameKind.Complete), first: false, cancellationToken)
                        .ConfigureAwait(false);
                    await destination.WriteAsync("]"u8.ToArray(), cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                    Completed.TrySetResult();
                    return;
                }

                await Write(destination, new Frame(FrameKind.Row, 1), first: false, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);

                await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (fail)
                {
                    await Write(
                        destination,
                        new Frame(FrameKind.Fault, Message: "provider failed after row 1"),
                        first: false,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await Write(destination, new Frame(FrameKind.Row, 2), first: false, cancellationToken).ConfigureAwait(false);
                    await Write(destination, new Frame(FrameKind.Log, Message: "row 2 read"), first: false, cancellationToken).ConfigureAwait(false);
                    await Write(destination, new Frame(FrameKind.Complete), first: false, cancellationToken).ConfigureAwait(false);
                }

                await destination.WriteAsync("]"u8.ToArray(), cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                Completed.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                Stopped.TrySetResult();
            }
        }

        private static async Task Write(
            Stream destination,
            Frame frame,
            bool first,
            CancellationToken cancellationToken)
        {
            if (!first)
            {
                await destination.WriteAsync(","u8.ToArray(), cancellationToken).ConfigureAwait(false);
            }

            await JsonSerializer.SerializeAsync(destination, frame, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private sealed class NodeFramedHandler(
        Func<CancellationToken, IAsyncEnumerable<DynamicValueNode>> nodes,
        TaskCompletionSource release) : HttpMessageHandler
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var pipe = new Pipe();
            _ = Produce(pipe.Writer, lifetime.Token);

            Stream body = new CancelOnDisposeStream(pipe.Reader.AsStream(), lifetime);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body),
            });
        }

        private async Task Produce(PipeWriter writer, CancellationToken cancellationToken)
        {
            await using Stream destination = writer.AsStream();
            await destination.WriteAsync("["u8.ToArray(), cancellationToken).ConfigureAwait(false);
            await Write(new NodeFrame(FrameKind.Start), first: true).ConfigureAwait(false);

            bool firstNode = true;
            await foreach (DynamicValueNode node in nodes(cancellationToken)
                .WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await Write(new NodeFrame(FrameKind.Row, node), first: false).ConfigureAwait(false);
                if (firstNode)
                {
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                    await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    firstNode = false;
                }
            }

            await Write(new NodeFrame(FrameKind.Complete), first: false).ConfigureAwait(false);
            await destination.WriteAsync("]"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            Completed.TrySetResult();

            async Task Write(NodeFrame frame, bool first)
            {
                if (!first)
                {
                    await destination.WriteAsync(","u8.ToArray(), cancellationToken).ConfigureAwait(false);
                }

                await JsonSerializer.SerializeAsync(destination, frame, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private sealed class CancelOnDisposeStream(Stream inner, CancellationTokenSource lifetime) : Stream
    {
        private int _disposed;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                lifetime.Cancel();
                inner.Dispose();
                lifetime.Dispose();
            }

            base.Dispose(disposing);
        }
    }

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

    private sealed class CountingBufferWriter : IBufferWriter<byte>
    {
        private readonly byte[] _buffer = new byte[16 * 1024];

        public long BytesWritten { get; private set; }

        public void Advance(int count) => BytesWritten += count;

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Assert.True(sizeHint <= _buffer.Length);
            return _buffer;
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Assert.True(sizeHint <= _buffer.Length);
            return _buffer;
        }
    }

    private sealed class StreamingGraphContext(DbContextOptions<StreamingGraphContext> options)
        : DbContext(options)
    {
        public DbSet<StreamingPost> Posts => Set<StreamingPost>();
    }

    private sealed class StreamingPost
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public List<StreamingTag> Tags { get; set; } = [];
    }

    private sealed class StreamingTag
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public List<StreamingPost> Posts { get; set; } = [];
    }
}
