// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Wire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using InfoCarrier.WireContract;
using Old = InfoCarrier.Core.Wire.V3.Common;
using OldNodes = InfoCarrier.Core.Wire.V3.Expressions;

namespace InfoCarrier.Core.TransportTests;

public class ProtocolMigrationTest(ITestOutputHelper output) : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddInfoCarrierWireProtocols().BuildServiceProvider();
    private InfoCarrierWireProtocol Protocol => _services.GetRequiredService<InfoCarrierWireProtocol>();
    private InfoCarrierEnvelopeServer Dispatcher(IInfoCarrierServer server, IInfoCarrierSerializer serializer)
        => ActivatorUtilities.CreateInstance<InfoCarrierEnvelopeServer>(_services, server, serializer);
    public void Dispose() => _services.Dispose();
    [Fact]
    public void Current_queries_embed_objects_and_old_queries_retain_encoded_bytes()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, Protocol);
        var request = new QueryDataRequest
        {
            ProtocolVersion = 4,
            Query = new ConstantNode { Type = new TypeNode { Name = "System.String" }, PrimitiveValue = new string('x', 1024) },
            TrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution,
            SplitQueryBehavior = QuerySplittingBehavior.SplitQuery,
            IsAsync = true, ReturnsSingleResult = true, TransactionId = "transaction",
        };
        byte[] current = Protocol.Serialize(serializer, request, 4);
        byte[] previous = Protocol.Serialize(serializer, request, 3);
        using JsonDocument live = JsonDocument.Parse(current);
        using JsonDocument old = JsonDocument.Parse(previous);
        Assert.Equal(JsonValueKind.Object, live.RootElement.GetProperty("query").ValueKind);
        Assert.False(live.RootElement.TryGetProperty("serializedQuery", out _));
        Assert.Equal(JsonValueKind.String, old.RootElement.GetProperty("serializedQuery").ValueKind);
        Assert.False(old.RootElement.TryGetProperty("query", out _));
        Assert.True(current.Length < previous.Length);
        output.WriteLine($"1024-character constant query: protocol 3={previous.Length} bytes; protocol 4={current.Length} bytes.");
        QueryDataRequest converted = Protocol.Deserialize<QueryDataRequest>(serializer, previous, 3)!;
        Assert.Equal(request.TrackingBehavior, converted.TrackingBehavior);
        Assert.Equal(request.SplitQueryBehavior, converted.SplitQueryBehavior);
        Assert.Equal(request.TransactionId, converted.TransactionId);
        Assert.True(converted.IsAsync);
        Assert.True(converted.ReturnsSingleResult);
        Assert.Equal(new string('x', 1024), Assert.IsType<ConstantNode>(converted.Query).PrimitiveValue?.ToString());
        Assert.Throws<JsonException>(() => serializer.Deserialize<QueryDataRequest>(previous));
    }

    [Fact]
    public void Old_response_conversion_preserves_references_nulls_and_materialization_markers()
    {
        var type = new TypeNode { Name = "Application.Item", EntityTypeName = "Item" };
        var result = new QueryDataResult
        {
            IsEntityResult = true, ElementTypeName = "Application.Item",
            Rows =
            [
                new DynamicValueNode
                {
                    Type = type, Id = 7, IsTracked = true,
                    EntityKey = new EntityKeyNode { EntityTypeName = "Item", KeyValues = [42] },
                    Properties = [new DynamicPropertyValue { Name = "Children", IsLoadedNavigation = true, IsJoinRows = true,
                        Value = new DynamicValueNode { Type = type, Ref = 7 } }],
                },
                new DynamicValueNode { Type = type, Ref = 7 },
                new DynamicValueNode { Type = type, IsNull = true },
                new DynamicValueNode { Type = type },
            ],
            ServerLog = [new ServerLogEvent { Category = "category", EventId = 19, Level = 2, Message = "message" }],
        };
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, Protocol);
        byte[] bytes = Protocol.Serialize(serializer, result, 3);
        Old.QueryDataResult old = serializer.Deserialize<Old.QueryDataResult>(bytes)!;
        List<OldNodes.DynamicValueNode> oldRows = JsonSerializer.Deserialize(old.SerializedResults,
            OldNodes.Protocol3ExpressionJsonContext.Default.ListDynamicValueNode)!;
        Assert.Equal(7, oldRows[0].Id);
        Assert.Equal(7, oldRows[1].Ref);
        Assert.True(oldRows[2].IsNull);
        Assert.False(oldRows[3].IsNull);
        Assert.True(oldRows[0].IsTracked);
        Assert.True(Assert.Single(oldRows[0].Properties).IsLoadedNavigation);
        Assert.True(Assert.Single(oldRows[0].Properties).IsJoinRows);
        Assert.Equal("42", Assert.Single(oldRows[0].EntityKey!.KeyValues)?.ToString());
        QueryDataResult restored = Protocol.Deserialize<QueryDataResult>(serializer, bytes, 3)!;
        Assert.Equal(7, restored.Rows[1].Ref);
        Assert.Equal("message", Assert.Single(restored.ServerLog!).Message);
        using JsonDocument current = JsonDocument.Parse(serializer.Serialize(result));
        Assert.Equal(JsonValueKind.Array, current.RootElement.GetProperty("rows").ValueKind);
        Assert.False(current.RootElement.TryGetProperty("serializedResults", out _));
    }

    [Fact]
    public async Task Unknown_old_expression_discriminator_cannot_reach_execution()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, Protocol);
        IInfoCarrierServer server = DispatchProxy.Create<IInfoCarrierServer, ProtocolCompatibilityTest.CountingServer>();
        var dispatcher = Dispatcher(server, serializer);
        var request = new Old.QueryDataRequest
        {
            SerializedQuery = "{\"$kind\":99999}"u8.ToArray(),
            TrackingBehavior = Old.QueryTrackingBehavior.NoTracking, IsAsync = true, ReturnsSingleResult = false,
        };
        InfoCarrierEnvelope response = await dispatcher.DispatchAsync(new InfoCarrierEnvelope
        {
            ProtocolVersion = 3, Operation = InfoCarrierOperation.Query, Payload = serializer.Serialize(request),
        }, TestContext.Current.CancellationToken);
        Assert.Equal(3, response.ProtocolVersion);
        Assert.NotNull(response.Fault);
        Assert.Equal(0, ((ProtocolCompatibilityTest.CountingServer)server).Calls);
    }

    [Theory]
    [InlineData("tracking")]
    [InlineData("splitting")]
    [InlineData("null-expression")]
    [InlineData("malformed-expression")]
    public async Task Invalid_historical_meanings_are_refused_before_execution(string mutation)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, Protocol);
        IInfoCarrierServer server = DispatchProxy.Create<IInfoCarrierServer, ProtocolCompatibilityTest.CountingServer>();
        var old = new Old.QueryDataRequest
        {
            ProtocolVersion = 3,
            SerializedQuery = JsonSerializer.SerializeToUtf8Bytes<OldNodes.ExpressionNode>(new OldNodes.ConstantNode
            {
                Type = new OldNodes.TypeNode { Name = typeof(int).AssemblyQualifiedName! }, PrimitiveValue = 1,
            }, OldNodes.Protocol3ExpressionJsonContext.Default.ExpressionNode),
            TrackingBehavior = Old.QueryTrackingBehavior.NoTracking, IsAsync = true, ReturnsSingleResult = false,
        };
        old = mutation switch
        {
            "tracking" => old with { TrackingBehavior = (Old.QueryTrackingBehavior)999 },
            "splitting" => old with { SplitQueryBehavior = (Old.QuerySplittingBehavior)999 },
            "null-expression" => old with { SerializedQuery = "null"u8.ToArray() },
            _ => old with { SerializedQuery = "{"u8.ToArray() },
        };
        InfoCarrierEnvelope response = await Dispatcher(server, serializer).DispatchAsync(new InfoCarrierEnvelope
        {
            ProtocolVersion = 3, Operation = InfoCarrierOperation.Query, Payload = serializer.Serialize(old),
            CorrelationId = "invalid-historical-meaning",
        }, TestContext.Current.CancellationToken);
        Assert.Equal(3, response.ProtocolVersion);
        Assert.Equal("invalid-historical-meaning", response.CorrelationId);
        Assert.NotNull(response.Fault);
        Assert.Contains("JsonException", response.Fault.TypeName, StringComparison.Ordinal);
        Assert.Equal(0, ((ProtocolCompatibilityTest.CountingServer)server).Calls);
    }

    [Fact]
    public void Historical_inner_graph_limit_is_checked_before_parsing()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, Protocol);
        var old = new Old.QueryDataRequest
        {
            ProtocolVersion = 3, SerializedQuery = Enumerable.Repeat((byte)'x', 257).ToArray(),
            TrackingBehavior = Old.QueryTrackingBehavior.NoTracking, IsAsync = true, ReturnsSingleResult = false,
        };
        var codec = new Protocol3WireProtocolCodec();
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            codec.Deserialize<QueryDataRequest>(serializer, serializer.Serialize(old), new InfoCarrierPayloadLimits(maxRequestBytes: 256)));
        Assert.Contains("protocol 3 expression", failure.Message, StringComparison.Ordinal);
        Assert.Contains("257", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Historical_stream_keeps_rows_before_a_terminal_fault()
    {
        using var destination = new MemoryStream();
        var codec = new Protocol3WireProtocolCodec();
        await codec.WriteFramesAsync(destination, Frames(), TestContext.Current.CancellationToken);
        destination.Position = 0;
        var frames = new List<Wire.V3.Query.QueryStreamFrame>();
        await foreach (Wire.V3.Query.QueryStreamFrame frame in Wire.V3.Query.QueryStreamFrameCodec.ReadAsync(destination, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }
        Assert.Equal(3, Assert.IsType<Wire.V3.Query.QueryStartFrame>(frames[0]).Version);
        Assert.Equal(37, Assert.IsType<JsonElement>(Assert.IsType<Wire.V3.Query.QueryRowFrame>(frames[1]).Value.PrimitiveValue).GetInt32());
        Old.InfoCarrierFault fault = Assert.IsType<Wire.V3.Query.QueryFaultFrame>(frames[2]).Fault;
        Assert.Equal("partial failure", fault.Message);
        Assert.Equal("inner failure", fault.Inner!.Message);
        Assert.Equal(new[] { 19 }, fault.FailedCorrelationIds);
        Assert.Equal(3, frames.Count);

        static async IAsyncEnumerable<Query.QueryStreamFrame> Frames()
        {
            yield return new Query.QueryStartFrame(4, 0, false, null);
            yield return new Query.QueryRowFrame(new DynamicValueNode
            {
                Type = new TypeNode { Name = typeof(int).AssemblyQualifiedName! }, PrimitiveValue = 37,
            });
            await Task.Yield();
            yield return new Query.QueryFaultFrame(new InfoCarrierFault
            {
                TypeName = typeof(InvalidOperationException).FullName!, Message = "partial failure", FailedCorrelationIds = [19],
                Inner = new InfoCarrierFault { TypeName = typeof(Exception).FullName!, Message = "inner failure" },
            });
        }
    }

    [Fact]
    public async Task Unknown_old_operation_is_echoed_as_fault_without_execution()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, Protocol);
        IInfoCarrierServer server = DispatchProxy.Create<IInfoCarrierServer, ProtocolCompatibilityTest.CountingServer>();
        byte[] bytes = serializer.Serialize(new Old.InfoCarrierEnvelope
        {
            ProtocolVersion = 3, Operation = (Old.InfoCarrierOperation)999, Payload = [], CorrelationId = "unknown-old",
        });
        using var source = new MemoryStream(bytes);
        InfoCarrierEnvelope request = (await Protocol.ReadEnvelopeAsync(serializer, source, TestContext.Current.CancellationToken))!;
        InfoCarrierEnvelope response = await Dispatcher(server, serializer).DispatchAsync(request, TestContext.Current.CancellationToken);
        Old.InfoCarrierEnvelope encoded = serializer.Deserialize<Old.InfoCarrierEnvelope>(
            Protocol.Serialize(serializer, response, 3))!;
        Assert.Equal(3, encoded.ProtocolVersion);
        Assert.Equal((Old.InfoCarrierOperation)999, encoded.Operation);
        Assert.Equal("unknown-old", encoded.CorrelationId);
        Assert.NotNull(encoded.Fault);
        Assert.Equal(0, ((ProtocolCompatibilityTest.CountingServer)server).Calls);
    }

    [Fact]
    public void Frozen_request_types_keep_request_limits_and_independent_nested_definitions()
    {
        Assert.True(InfoCarrierPayloadLimits.IsRequest<Old.QueryDataRequest>());
        Assert.True(InfoCarrierPayloadLimits.IsRequest<Old.InfoCarrierEnvelope>());
        Assert.True(InfoCarrierPayloadLimits.IsRequest<Old.SaveChangesRequest>());
        Assert.True(InfoCarrierPayloadLimits.IsRequest<Old.SavepointRequest>());
        var serializer = new SystemTextJsonInfoCarrierSerializer(new InfoCarrierPayloadLimits(maxRequestBytes: 8), Protocol);
        Assert.Throws<InvalidOperationException>(() => serializer.Deserialize<Old.QueryDataRequest>(new byte[9]));
        Assert.Equal(typeof(Old.QueryTrackingBehavior), typeof(Old.QueryDataRequest).GetProperty("TrackingBehavior")!.PropertyType);
        Assert.Equal(typeof(OldNodes.TypeNode), typeof(OldNodes.ConstantNode).GetProperty("Type")!.PropertyType);
        Assert.Equal(3, Old.InfoCarrierEnvelope.CurrentProtocolVersion);
        Assert.Equal(3, Wire.V3.Query.QueryStreamFrameCodec.Version);
        Assert.Equal(4, InfoCarrierEnvelope.CurrentProtocolVersion);
    }

    [Fact]
    public async Task Old_stream_body_identification_is_checked_before_inner_graph_decoding()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, Protocol);
        byte[] body = serializer.Serialize(new Old.QueryDataRequest
        {
            ProtocolVersion = 0, SerializedQuery = "invalid graph"u8.ToArray(),
            TrackingBehavior = Old.QueryTrackingBehavior.NoTracking, IsAsync = true, ReturnsSingleResult = false,
        });
        using var source = new MemoryStream(body);
        InfoCarrierProtocolException failure = await Assert.ThrowsAsync<InfoCarrierProtocolException>(async () =>
            await Protocol.ReadQueryAsync(serializer, source, 3, TestContext.Current.CancellationToken));
        Assert.Equal(0, failure.ClientVersion);
    }

    [Theory]
    [InlineData("field")]
    [InlineData("frame")]
    public async Task Raising_the_live_integer_does_not_waive_existing_protocol_3_breaks(string mutation)
    {
        var previous = ContractInspector.Inspect([1, 2, 3, 4], await FrameContracts.InspectAsync());
        var current = previous with
        {
            Version = 5, AllocatedVersions = [1, 2, 3, 4, 5],
            Types = previous.Types.ToDictionary(p => p.Key, p => p.Value with
                { Properties = p.Value.Properties.ToDictionary(q => q.Key, q => q.Value) }),
            Frames = previous.Frames.ToDictionary(p => p.Key, p => p.Value.ToArray()),
        };
        if (mutation == "field")
        {
            current.Types["InfoCarrier.Core.Wire.V3.Protocol3JsonContext/InfoCarrier.Core.Wire.V3.Common.QueryDataRequest"]
                .Properties.Remove("transactionId");
        }
        else { current.Frames["V3/QueryStartFrame/scalar"][1] = "Slot:int32"; }
        ComparisonResult result = ContractComparer.Compare(previous, current, "protected-protocol-3");
        Assert.False(result.Passed);
        Assert.Contains(result.AllocationErrors, error => error.Contains("Supported protocol 3", StringComparison.Ordinal));
    }
}
