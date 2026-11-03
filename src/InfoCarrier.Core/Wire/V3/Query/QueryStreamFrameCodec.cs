// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using InfoCarrier.Core.Wire.V3.Common;
using InfoCarrier.Core.Wire.V3.Expressions;

namespace InfoCarrier.Core.Wire.V3.Query;

/// <summary>
///     Reads and writes compact tagged query frames.
/// </summary>
public static class QueryStreamFrameCodec
{
    // Preserve the original default element reader options without reflection-based metadata.
    private static readonly JsonTypeInfo<JsonElement> FrameElement = JsonMetadataServices.CreateValueInfo<JsonElement>(
        new JsonSerializerOptions { TypeInfoResolver = JsonTypeInfoResolver.Combine() }, JsonMetadataServices.JsonElementConverter);

    /// <summary>
    ///     The current query stream protocol version.
    /// </summary>
    public const int Version = InfoCarrierEnvelope.CurrentProtocolVersion;

    /// <summary>
    ///     Writes a complete query frame sequence to <paramref name="destination" />.
    /// </summary>
    public static async ValueTask WriteAsync(
        Stream destination,
        IAsyncEnumerable<QueryStreamFrame> frames,
        CancellationToken cancellationToken = default)
    {
        await destination.WriteAsync("["u8.ToArray(), cancellationToken).ConfigureAwait(false);

        bool started = false;
        bool terminated = false;
        var buffer = new ArrayBufferWriter<byte>();
        await foreach (QueryStreamFrame frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (terminated)
            {
                throw new InvalidOperationException("A query stream cannot contain frames after its terminal frame.");
            }

            if (!started && frame is not QueryStartFrame)
            {
                throw new InvalidOperationException("A query stream must start with a start frame.");
            }

            buffer.Clear();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                Write(writer, frame);
            }

            if (started)
            {
                await destination.WriteAsync(","u8.ToArray(), cancellationToken).ConfigureAwait(false);
            }

            await destination.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            started = true;
            terminated = frame is QueryCompleteFrame or QueryFaultFrame;
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!started)
        {
            throw new InvalidOperationException("A query stream cannot be empty.");
        }

        if (!terminated)
        {
            throw new InvalidOperationException("A query stream must end with a terminal frame.");
        }

        await destination.WriteAsync("]"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Reads query frames as they become available from <paramref name="source" />.
    /// </summary>
    public static async IAsyncEnumerable<QueryStreamFrame> ReadAsync(
        Stream source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        bool started = false;
        bool terminated = false;

        await foreach (JsonElement element in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(
            source,
            FrameElement,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (terminated)
            {
                throw new JsonException("The query stream contains a frame after its terminal frame.");
            }

            if (!started
                && (element.ValueKind != JsonValueKind.Array
                    || element.GetArrayLength() == 0
                    || element[0].ValueKind != JsonValueKind.Number
                    || element[0].GetInt32() != 0))
            {
                throw new JsonException("The query stream does not start with a start frame.");
            }

            QueryStreamFrame frame = Read(element);

            if (frame is QueryStartFrame start && start.Version != Version)
            {
                throw new InfoCarrierProtocolException(Version, start.Version);
            }

            started = true;
            terminated = frame is QueryCompleteFrame or QueryFaultFrame;
            yield return frame;
        }

        if (!started)
        {
            throw new JsonException("The query stream is empty.");
        }

        if (!terminated)
        {
            throw new JsonException("The query stream ended without a terminal frame.");
        }
    }

    private static void Write(Utf8JsonWriter writer, QueryStreamFrame frame)
    {
        writer.WriteStartArray();
        switch (frame)
        {
            case QueryStartFrame start:
                writer.WriteNumberValue(0);
                writer.WriteNumberValue(start.Version);
                writer.WriteNumberValue(start.Slot);
                writer.WriteBooleanValue(start.IsEntityResult);
                writer.WriteStringValue(start.ElementTypeName);
                break;

            case QueryRowFrame row:
                writer.WriteNumberValue(1);
                ((JsonConverter<DynamicValueNode>)Protocol3ExpressionJsonContext.Default.DynamicValueNode.Converter)
                    .Write(writer, row.Value, Protocol3ExpressionJsonContext.Default.Options);
                break;

            case QueryLogFrame log:
                writer.WriteNumberValue(2);
                ((JsonConverter<ServerLogEvent>)Protocol3JsonContext.Default.ServerLogEvent.Converter)
                    .Write(writer, log.Event, Protocol3JsonContext.Default.Options);
                break;

            case QueryCompleteFrame:
                writer.WriteNumberValue(3);
                break;

            case QueryFaultFrame fault:
                writer.WriteNumberValue(4);
                ((JsonConverter<InfoCarrierFault>)Protocol3JsonContext.Default.InfoCarrierFault.Converter)
                    .Write(writer, fault.Fault, Protocol3JsonContext.Default.Options);
                break;

            default:
                throw new InvalidOperationException($"Unknown query frame type '{frame.GetType()}'.");
        }

        writer.WriteEndArray();
    }

    private static QueryStreamFrame Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
        {
            throw new JsonException("A query frame must be a non-empty JSON array.");
        }

        int tag = element[0].GetInt32();
        return tag switch
        {
            0 when element.GetArrayLength() == 5 => new QueryStartFrame(
                element[1].GetInt32(),
                element[2].GetInt32(),
                element[3].GetBoolean(),
                element[4].ValueKind == JsonValueKind.Null ? null : element[4].GetString()),
            1 when element.GetArrayLength() == 2 => new QueryRowFrame(
                element[1].Deserialize(Protocol3ExpressionJsonContext.Default.DynamicValueNode)
                    ?? throw new JsonException("A row frame contains a null value.")),
            2 when element.GetArrayLength() == 2 => new QueryLogFrame(
                element[1].Deserialize(Protocol3JsonContext.Default.ServerLogEvent)
                    ?? throw new JsonException("A log frame contains a null event.")),
            3 when element.GetArrayLength() == 1 => new QueryCompleteFrame(),
            4 when element.GetArrayLength() == 2 => new QueryFaultFrame(
                element[1].Deserialize(Protocol3JsonContext.Default.InfoCarrierFault)
                    ?? throw new JsonException("A fault frame contains a null fault.")),
            _ => throw new JsonException($"Unknown or malformed query frame tag '{tag}'."),
        };
    }
}
