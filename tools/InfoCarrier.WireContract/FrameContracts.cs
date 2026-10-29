// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;

namespace InfoCarrier.WireContract;

internal static class FrameContracts
{
    internal static async Task<Dictionary<string, string[]>> InspectAsync()
    {
        int version = InfoCarrierEnvelope.CurrentProtocolVersion;
        int slot = version == 97 ? 98 : 97;
        var row = new QueryRowFrame(new DynamicValueNode
        {
            Type = new TypeNode { Name = "System.Int32" },
            PrimitiveValue = 37,
        });
        var log = new QueryLogFrame(new ServerLogEvent
        {
            Level = 2,
            EventId = 113,
            EventName = "frame-event",
            Category = "frame-category",
            Message = "frame-message",
        });
        var fault = new QueryFaultFrame(new InfoCarrierFault
        {
            TypeName = "System.InvalidOperationException",
            Message = "frame-fault",
            StackTrace = "frame-stack",
            FailedCorrelationIds = [19, 23],
            Inner = new InfoCarrierFault { TypeName = "System.Exception", Message = "frame-inner" },
        });
        QueryStreamFrame[] success = [new QueryStartFrame(version, slot, false, null), row, log, new QueryCompleteFrame()];
        QueryStreamFrame[] failure = [new QueryStartFrame(version, slot, true, "frame-element"), fault];
        var represented = success.Concat(failure).Select(frame => frame.GetType()).ToHashSet();
        var discovered = typeof(QueryStreamFrame).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(QueryStreamFrame).IsAssignableFrom(type)).ToHashSet();
        if (!represented.SetEquals(discovered))
        {
            throw Incomplete("types", "unrepresented frame types: "
                + string.Join(", ", discovered.Except(represented).Select(type => type.FullName)));
        }

        var layouts = new Dictionary<string, string[]>(StringComparer.Ordinal);
        await InspectSequenceAsync(success, "scalar", layouts).ConfigureAwait(false);
        await InspectSequenceAsync(failure, "entity", layouts).ConfigureAwait(false);
        return layouts;
    }

    private static async Task InspectSequenceAsync(
        QueryStreamFrame[] expected, string variant, Dictionary<string, string[]> layouts)
    {
        using var stream = new MemoryStream();
        await QueryStreamFrameCodec.WriteAsync(stream, Frames(expected)).ConfigureAwait(false);
        using var document = JsonDocument.Parse(stream.ToArray());
        if (document.RootElement.ValueKind != JsonValueKind.Array
            || document.RootElement.GetArrayLength() != expected.Length)
        {
            throw Incomplete(variant, "writer changed the enclosing array or frame count");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            QueryStreamFrame frame = expected[index];
            string name = frame.GetType().Name + (frame is QueryStartFrame ? "/" + variant : "");
            layouts.Add(name, Describe(document.RootElement[index], frame));
        }

        stream.Position = 0;
        var actual = await QueryStreamFrameCodec.ReadAsync(stream).ToListAsync().ConfigureAwait(false);
        if (actual.Count != expected.Length)
        {
            throw Incomplete(variant, "reader changed the frame count");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            QueryStreamFrame original = expected[index];
            QueryStreamFrame decoded = actual[index];
            bool equal = original.GetType() == decoded.GetType()
                && (original is QueryStartFrame or QueryCompleteFrame
                    ? original.Equals(decoded)
                    : JsonElement.DeepEquals(Payload(original)!.Value.Value, Payload(decoded)!.Value.Value));
            if (!equal)
            {
                throw Incomplete(original.GetType().Name, "reader no longer restores the named fields and payload");
            }
        }
    }

    private static string[] Describe(JsonElement encoded, QueryStreamFrame frame)
    {
        if (encoded.ValueKind != JsonValueKind.Array || encoded.GetArrayLength() == 0)
        {
            throw Incomplete(frame.GetType().Name, "writer no longer emits a nonempty tagged array");
        }

        var payload = Payload(frame);
        return encoded.EnumerateArray().Select(element =>
        {
            if (frame is QueryStartFrame start)
            {
                if (element.ValueKind == JsonValueKind.Number && element.GetInt32() == start.Version)
                    return "Version:int32";
                if (element.ValueKind == JsonValueKind.Number && element.GetInt32() == start.Slot)
                    return "Slot:int32";
                if (element.ValueKind is JsonValueKind.True or JsonValueKind.False
                    && element.GetBoolean() == start.IsEntityResult)
                    return "IsEntityResult:boolean";
                if (element.ValueKind == JsonValueKind.Null && start.ElementTypeName is null
                    || element.ValueKind == JsonValueKind.String && element.GetString() == start.ElementTypeName)
                    return "ElementTypeName:string?";
            }

            // The metadata comparator owns payload evolution. This check only accepts the actual
            // payload representation when the handwritten codec agrees with that same metadata.
            if (payload is { } contained && JsonElement.DeepEquals(element, contained.Value))
                return "Payload:" + contained.Type;
            if (element.ValueKind == JsonValueKind.Number)
                return "Tag:" + element.GetRawText();
            throw Incomplete(frame.GetType().Name, "unclassified value or payload representation: " + element.GetRawText());
        }).ToArray();
    }

    private static (string Type, JsonElement Value)? Payload(QueryStreamFrame frame) => frame switch
    {
        QueryRowFrame row => Encode(row.Value, ExpressionJsonContext.Default.DynamicValueNode),
        QueryLogFrame log => Encode(log.Event, InfoCarrierJsonContext.Default.ServerLogEvent),
        QueryFaultFrame fault => Encode(fault.Fault, InfoCarrierJsonContext.Default.InfoCarrierFault),
        _ => null,
    };

    private static (string Type, JsonElement Value) Encode<T>(T value, JsonTypeInfo<T> metadata) =>
        (metadata.Type.FullName!, JsonSerializer.SerializeToElement(value, metadata));

    private static InvalidOperationException Incomplete(string path, string reason) =>
        new($"Incomplete wire coverage at stream/{path}: {reason}. Extend the authored frame probe; a version increase does not resolve this.");

    private static async IAsyncEnumerable<QueryStreamFrame> Frames(QueryStreamFrame[] frames)
    {
        foreach (QueryStreamFrame frame in frames)
            yield return frame;
        await Task.CompletedTask;
    }
}
